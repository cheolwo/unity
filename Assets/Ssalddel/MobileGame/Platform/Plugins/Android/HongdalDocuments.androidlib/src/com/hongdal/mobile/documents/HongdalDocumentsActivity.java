package com.hongdal.mobile.documents;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import org.json.JSONObject;
import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.nio.ByteBuffer;
import java.nio.charset.CodingErrorAction;
import java.nio.charset.StandardCharsets;

/** Owns one user-selected document operation. Does not retain, log, or publish document URIs. */
public final class HongdalDocumentsActivity extends Activity {
    private static final int DOCUMENT_REQUEST = 4719;
    private static final int MAXIMUM_BYTES = 1024 * 1024;
    private static ActiveRequest active;
    private String operationId;
    private boolean pickerLaunched;
    private boolean completed;

    private static final class ActiveRequest {
        final String receiver, id, kind, fileName, text, mime;
        ActiveRequest(String receiver, String id, String kind, String fileName, String text, String mime) {
            this.receiver=receiver; this.id=id; this.kind=kind; this.fileName=fileName; this.text=text; this.mime=mime;
        }
    }

    public static void open(final Activity source, final String receiver, final String id, final String kind,
                            final String fileName, final String text, final String mime) {
        synchronized (HongdalDocumentsActivity.class) {
            if (active != null) {
                send(receiver,id,"error","","앞선 문서 선택을 마쳐 주세요.");
                return;
            }
            if (!("read".equals(kind) || "write".equals(kind))) {
                send(receiver,id,"error","","지원하지 않는 문서 작업입니다."); return;
            }
            if (text != null && text.getBytes(StandardCharsets.UTF_8).length > MAXIMUM_BYTES) {
                send(receiver,id,"error","","자료는 UTF-8 기준 1MB 이하만 처리할 수 있습니다."); return;
            }
            active = new ActiveRequest(receiver,id,kind,fileName,text,mime);
        }
        source.runOnUiThread(new Runnable() { @Override public void run() {
            try {
                Intent bridge = new Intent(source, HongdalDocumentsActivity.class);
                bridge.putExtra("operationId",id);
                source.startActivity(bridge);
            } catch (Exception ignored) { finishRequest(id,"error","","문서 선택창을 열지 못했습니다."); }
        }});
    }

    public static synchronized void abandon(String id) {
        if (active != null && active.id.equals(id)) active=null;
    }

    @Override protected void onCreate(Bundle state) {
        super.onCreate(state);
        operationId=getIntent().getStringExtra("operationId");
        final ActiveRequest request=current();
        if (request==null) { completed=true; finish(); return; }
        pickerLaunched=state!=null && state.getBoolean("pickerLaunched",false);
        if (pickerLaunched) return;
        pickerLaunched=true;
        try {
            Intent picker=new Intent("read".equals(request.kind)?Intent.ACTION_OPEN_DOCUMENT:Intent.ACTION_CREATE_DOCUMENT);
            picker.addCategory(Intent.CATEGORY_OPENABLE);
            picker.setType(request.mime==null || request.mime.isEmpty()?"application/json":request.mime);
            if ("write".equals(request.kind)) picker.putExtra(Intent.EXTRA_TITLE,request.fileName);
            startActivityForResult(picker,DOCUMENT_REQUEST);
        } catch (Exception ignored) { complete("error","","문서 선택창을 열지 못했습니다."); }
    }

    @Override protected void onSaveInstanceState(Bundle state) {
        state.putBoolean("pickerLaunched",pickerLaunched);
        super.onSaveInstanceState(state);
    }

    private synchronized ActiveRequest current() {
        synchronized (HongdalDocumentsActivity.class) {
            return active!=null && active.id.equals(operationId)?active:null;
        }
    }

    @Override protected void onActivityResult(int requestCode,int resultCode,Intent data) {
        super.onActivityResult(requestCode,resultCode,data);
        if (requestCode!=DOCUMENT_REQUEST || completed) return;
        if (resultCode!=RESULT_OK || data==null || data.getData()==null) { complete("cancelled","",""); return; }
        final Uri uri=data.getData();
        if (!"content".equals(uri.getScheme())) { complete("error","","시스템 문서 제공자가 반환한 자료만 열 수 있습니다."); return; }
        final ActiveRequest request=current();
        if (request==null) { completed=true; finish(); return; }
        new Thread(new Runnable() { @Override public void run() {
            String value="";
            try {
                if ("read".equals(request.kind)) {
                    try (InputStream stream=getContentResolver().openInputStream(uri);
                         ByteArrayOutputStream memory=new ByteArrayOutputStream()) {
                        if (stream==null) throw new java.io.IOException();
                        byte[] buffer=new byte[8192]; int read;
                        while ((read=stream.read(buffer))!=-1) {
                            if (memory.size()+read>MAXIMUM_BYTES) throw new java.io.IOException();
                            memory.write(buffer,0,read);
                        }
                        value=StandardCharsets.UTF_8.newDecoder().onMalformedInput(CodingErrorAction.REPORT)
                            .onUnmappableCharacter(CodingErrorAction.REPORT).decode(ByteBuffer.wrap(memory.toByteArray())).toString();
                        if (value.startsWith("\uFEFF")) value=value.substring(1);
                    }
                } else {
                    byte[] bytes=(request.text==null?"":request.text).getBytes(StandardCharsets.UTF_8);
                    if (bytes.length>MAXIMUM_BYTES) throw new java.io.IOException();
                    try (OutputStream stream=getContentResolver().openOutputStream(uri,"w")) {
                        if (stream==null) throw new java.io.IOException();
                        stream.write(bytes);stream.flush();
                    }
                }
                final String output=value;
                runOnUiThread(new Runnable() { @Override public void run() { complete("ok",output,""); }});
            } catch (Exception ignored) {
                runOnUiThread(new Runnable() { @Override public void run() {
                    complete("error","","문서를 읽거나 저장하지 못했습니다. UTF-8 JSON과 1MB 크기 제한을 확인해 주세요.");
                }});
            }
        }},"HongdalDocumentIO").start();
    }

    private void complete(String status,String value,String error) {
        if (completed) return;
        completed=true;
        finishRequest(operationId,status,value,error);
        finish();
    }

    private static void finishRequest(String id,String status,String value,String error) {
        ActiveRequest request;
        synchronized (HongdalDocumentsActivity.class) {
            request=active;
            if (request==null || !request.id.equals(id)) return;
            active=null;
        }
        send(request.receiver,id,status,value,error);
    }

    private static void send(String receiver,String id,String status,String value,String error) {
        try {
            JSONObject result=new JSONObject();result.put("operationId",id);result.put("status",status);
            result.put("text",value);result.put("error",error);
            Class<?> player=Class.forName("com.unity3d.player.UnityPlayer");
            player.getMethod("UnitySendMessage",String.class,String.class,String.class)
                .invoke(null,receiver,"OnDocumentResult",result.toString());
        } catch (Exception ignored) { /* Document text and URI are deliberately never logged. */ }
    }

    @Override protected void onDestroy() {
        if (isFinishing() && !completed) {
            completed=true;
            finishRequest(operationId,"cancelled","","");
        }
        super.onDestroy();
    }
}
