"""동 디오라마 메시 자원 줄이기(지도 자료 적용 S2 첫 조각, 2026-11-16): 굽은 .bytes를 손실 없이 deflate로 싼다.

굽기 도구(make_dong_modules.py)의 결과 폴더를 읽어, 동 메시(hongdal-dong-<코드>.bytes)만 싸서 새 폴더에 쓴다.
목록(hongdal-dong-modules.json)의 bytes · sha256은 싼 파일의 값으로 바꾸고, 원래 값은 rawBytes · rawSha256으로 남긴다.
건물 색인 · 길 그래프는 그대로 옮긴다. 싼 파일의 꼴(작은 끝):
  'HDIZ' · int32 판(1) · int32 원래 길이 · 원래 SHA-256 32바이트 · raw deflate(RFC 1951) 본문
게임은 머리가 'HDIZ'이면 풀고, 원래 길이 · 해시가 맞을 때만 읽는다(행정동모듈Policy.UnpackModule). 'HDIO'(안 싼 것)도 그대로 읽는다.
면목 6동 메시 기준 67 MB → 약 13 MB(19 %, 손실 없음). 사직동 15.3 MB → 약 3 MB. 서울 427동으로 넓힐 때의 용량(정할 것 M4)을 맞추려는 첫 조각이다.
사용: python3 -I pack_dong_resources.py <굽기 결과 폴더> <싼 결과 폴더>
"""
import sys, os, json, zlib, struct, hashlib, shutil
SRC, DST = sys.argv[1:3]
os.makedirs(DST, exist_ok=True)
def sha(b): return hashlib.sha256(b).hexdigest().upper()
man_path = os.path.join(SRC, 'hongdal-dong-modules.json')
man = json.load(open(man_path, encoding='utf-8'))
total_raw = total_packed = 0
for m in man['modules']:
    raw = open(os.path.join(SRC, m['resource'] + '.bytes'), 'rb').read()
    assert raw[:4] == b'HDIO', m['resource']
    assert len(raw) == m['bytes'] and sha(raw) == m['sha256'].upper(), ('목록과 다름', m['resource'])
    c = zlib.compressobj(9, zlib.DEFLATED, -15, 9)
    body = c.compress(raw) + c.flush()
    assert zlib.decompress(body, -15) == raw
    packed = b'HDIZ' + struct.pack('<ii', 1, len(raw)) + bytes.fromhex(m['sha256']) + body
    open(os.path.join(DST, m['resource'] + '.bytes'), 'wb').write(packed)
    m['rawBytes'], m['rawSha256'] = m['bytes'], m['sha256'].upper()
    m['bytes'], m['sha256'] = len(packed), sha(packed)
    total_raw += len(raw); total_packed += len(packed)
    print(m['resource'], len(raw), '->', len(packed))
man['packing'] = 'deflate: HDIZ v1 (원래 길이 · SHA-256 머리 + raw deflate). rawBytes · rawSha256 = 싸기 전 HDIO 메시'
with open(os.path.join(DST, 'hongdal-dong-modules.json'), 'w', encoding='utf-8', newline='\n') as f: json.dump(man, f, ensure_ascii=False, indent=1)
for name in ('hongdal-dong-buildings.json', 'hongdal-dong-routes.bytes', 'hongdal-dong-stations.json'):
    if os.path.exists(os.path.join(SRC, name)): shutil.copyfile(os.path.join(SRC, name), os.path.join(DST, name))
print('total', total_raw, '->', total_packed, round(total_packed / total_raw, 3))
