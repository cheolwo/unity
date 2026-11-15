using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 3D 기반 A4b (2026-11-14): 보는 방향을 바꾼 화면을 게임 창을 들여다보지 않고도 확인하기 위한 촬영.
    //   HongdalSimulation.exe --hongdal-view-proof=<폴더>
    // 로 켜면 새 회차를 메모리에서만 열고(저장한 회차를 읽지도 쓰지도 않는다: SagajeongDispatchEngine.persistent),
    // 정해 둔 방향마다 한 동을 화면에 맞춘 뒤 그림을 찍어 폴더에 두고 스스로 꺼진다.
    // 방향마다 「옥상 가운데를 누르면 그 건물이 골라지는가」도 세어 view-proof.json에 적는다.
    // 표식을 놓는 계산과 건물을 고르는 계산이 그 방향에서도 서로 맞는지 보는 것이다.
    // 기존 검증 촬영(--sagajeong-…)과 달리 지금의 첫 화면(2.5D, 행정동, 정리한 화면)을 그대로 찍는다.
    // 방향 촬영은 시계를 돌리지 않고 명령을 보내지 않는다. 뒤에 붙은 장면(오늘 결산 확인, 운영 회차, 관리 건물)은
    // 저장하지 않는 촬영 세션 안에서만 명령을 보낸다.
    public sealed partial class SagajeongDispatchView
    {
        internal const string ViewProofFlag = "--hongdal-view-proof=";
        private const float ViewProofTimeoutSeconds = 170f;

        private static readonly (double Yaw, double Pitch, string File)[] ViewProofShots =
        {
            (30, 55, "01-yaw030-pitch55-default.png"),
            (120, 55, "02-yaw120-pitch55.png"),
            (210, 55, "03-yaw210-pitch55.png"),
            (300, 55, "04-yaw300-pitch55.png"),
            (30, 35, "05-yaw030-pitch35-low.png"),
            (30, 90, "06-yaw030-pitch90-top.png"),
        };

        [Serializable]
        private sealed class ViewProofReceipt
        {
            public bool passed;
            public string failure;
            public int screenWidth;
            public int screenHeight;
            public List<ViewProofShot> shots = new List<ViewProofShot>();
            public int dailyReportEntries;        // 오늘 날짜로 묶인 원장 줄 수 (확인 전)
            public bool dailyReportConfirmed;     // 확인한 뒤 읽음으로 보이는가
            public bool dailyReportUnchanged;     // 확인 전후로 시계와 원장이 그대로인가
            public bool roundOneBound;            // r187: 배차한 주문이 1회차의 대상으로 묶였는가
            public bool roundBlockedWhileRunning; // 진행 중에는 마감이 되지 않는가
            public bool roundCloseUnchanged;      // 마감과 다음 준비 전후로 시계·원장·커널 저장이 그대로인가
            public bool roundTwoDifferent;        // 2회차의 대상이 1회차와 다른가
            public bool roundWorldKept;           // 두 회차 동안 원장이 지워지지 않았는가
            public int roundsClosed;
            public string roundTargets;
            public int managedRoleBuildings;      // r143: 밝힌 내 건물(역할표) 수
            public int managedPartnerSites;       // 제휴 음식점의 가상 자리로 밝힌 건물 수 (제휴를 더 맺기 전)
            public int managedPartnersAdded;      // 촬영에서 더 맺은 제휴 수
            public int managedPartnerSitesAfter;  // 더 맺은 뒤 밝힌 가상 자리 수
            public int perspectiveRoofsSampled;   // A6 원근 보기: 옥상 가운데가 화면에 들어온 건물 가운데 센 수
            public int perspectiveRoofsPicked;    // 그 점을 눌렀을 때 같은 건물이 골라진 수
            public bool perspectiveUnchanged;     // 원근을 켜고 끈 전후로 시계·원장·커널 저장이 그대로인가
            public int stationRoofsSampled;       // 역 중심 보기: 판 안에서 옥상 가운데가 화면에 들어온 건물 가운데 센 수
            public int stationRoofsPicked;        // 그 점을 눌렀을 때 같은 건물이 골라진 수
            public int stationDongs;              // 판 안에 이름표가 선 동의 수
            public float stationCoverage;         // 첫 역의 판에서 자료가 덮는 넓이의 비율
            public float stationNextCoverage;     // 다음 역의 판에서의 비율
            public bool stationUnchanged;         // 역 중심 보기를 켜고 끈 전후로 시계·원장·커널 저장이 그대로인가
            public int pastReportsBefore;         // r174: 지난 날짜에 확인하지 않은 보고의 수 (어제 기록을 만들기 전)
            public int pastReportsPending;        // 어제 날짜로 기록 한 줄을 만든 뒤
            public int pastReportsAfterConfirm;   // 어제 보고를 확인한 뒤
            public bool pastReportUnchanged;      // 그 확인이 시계와 원장을 바꾸지 않았는가
            public int roamRiders;                // R4: 지도에 나타난 기사 표식 수
            public int roamGraphNodes;            // 읽은 길 그래프의 마디 수
            public float roamMetres;              // 표현의 시간 90초 동안 달린 거리 (m)
            public int roamJobs;                  // 그동안 마친 가상의 차례
            public int roamInsideBuildings;       // 그동안 기사 자리가 건물 안이었던 횟수 (0이어야 한다)
            public bool roamUnchanged;            // 기사가 돌아다녀도 시계와 원장이 그대로인가
            public int roamLitCells;              // 빛: 90초 뒤 기사의 자취나 온기로 밝은 칸의 수
            public int roamLightKinds;            // 빛이 적힌 겹의 수 (기사 · 음식점 · 전달지 · 온기)
            public int roamRidersMore;            // 촬영에서만 빛을 더 세운 뒤의 수 (고용이 아니다)
            public int roamLitCellsMore;          // 그 뒤 60초가 지났을 때 밝은 칸의 수 (더 넓어야 한다)
            public int roamRidersAfter;           // 더 세운 빛을 걷은 뒤의 수 (처음과 같아야 한다)
            public float roamLightMs;             // 빛을 한 번 고쳐 올리는 데 든 시간 (ms, 빛이 가장 많을 때의 평균)
            public int lightPlaces;               // 이름표 대신 빛으로 그린 곳의 수 (매장 · 배송지 · 역할 건물 · 제휴 음식점 · 배차 기사)
            public bool lightCard;                // 빛2: 고용한 기사의 카드가 오른쪽 칸 자리에 떴는가
            public int lightMenuRows;             // 빛2: 매장의 차림표에 선 줄 수
            public string businessDong;           // 사2: 기사 운영 창이 말한 동의 이름
            public float lightBeatStep;           // 빛의 숨: 두 장면 사이에 숨의 시계가 흐른 시간 (초. 게임 시계는 멈춘 채다)
            public bool deskOpen;                 // 동 관리 창: 열렸는가
            public string deskDong;               // 창이 말한 동의 이름
            public int deskPlaces;                // 「이 동의 내 곳」에 선 줄 수
            public int deskStepped;               // ◀ ▶로 넘긴 횟수 (제휴를 더 맺을 수 있는 동을 찾느라)
            public float deskScrolled;            // 묶음을 모두 편 뒤 끝까지 내린 만큼 (0이면 목록이 넘치지 않았다)
            public bool deskUnchanged;            // 열고 접고 펴고 넘기는 동안 시계와 원장이 그대로인가
            public bool deskContractTried;        // 창의 「건설 · 제휴 음식점」을 보낼 수 있었는가
            public string deskContractDong;       // 제휴를 보낸 동
            public int deskContractAdded;         // 그 뒤 늘어난 제휴 수
            public long deskContractSpent;        // 그 뒤 줄어든 자금
            public long deskContractCost;         // 그때의 제휴 값
            public int crowdLights;               // 빛이 몰렸을 때: 고른 동의 음식점 빛 수(촬영에서 더 켠 가상 자리 포함)
            public int crowdNeighbours;           // 한 빛의 80 m 안에 있는 다른 음식점 빛의 가장 큰 수
            public int crowdPairs;                // 60 m 안의 빛 짝 수
            public float crowdPhaseBefore;        // 그 짝들의 숨의 어긋남 (0 = 함께, 1 = 정반대). 자리로만 엇갈릴 때
            public float crowdPhaseAfter;         // 빛마다 엇갈릴 때
            public float crowdDimMin;             // 가장 많이 옅어진 연한 부분의 배율
            public int crowdCoresOnTop;           // 맨 위에 모아 그린 가운데 점 수
            public bool crowdUnchanged;           // 촬영 전후로 시계와 원장이 그대로인가
            public bool musicFound;               // 음악: 시험 곡 두 벌(music-proof/proof-120.wav · .mp3)이 있는가
            public bool musicWavOk;               // wav에서 120 BPM과 첫 박(0.5초)을 찾았는가
            public float musicWavBpm;
            public float musicWavFirstBeat;
            public float musicWavConfidence;
            public float musicWavSeconds;         // 곡의 길이
            public long musicWavAnalysisMs;       // 분석에 걸린 시간
            public string musicWavError;
            public bool musicMp3Loaded;           // mp3를 읽고 풀었는가
            public bool musicMp3Ok;
            public float musicMp3Bpm;
            public float musicMp3FirstBeat;
            public float musicMp3Confidence;
            public string musicMp3Error;
            public float musicPulsePeak;          // 박에 맞는 순간의 기사 빛 숨 (1에 가까울수록 크게 퍼짐)
            public float musicPulseRest;          // 한 박 뒤 (-1에 가까울수록 움츠러듦)
            public float musicBassAtBeat;         // 박에 맞는 순간의 저음
            public int musicSceneStores;          // 촬영에서만 더 켠 음식점 가상 자리
            public int musicSceneRiders;          // 그 장면의 기사 빛 수
            public bool settingsEscReady;         // 게임 창을 찾아 키 · 끌어다 놓기 메시지를 보낼 수 있었나
            public bool settingsEscOpened;        // ESC 키 메시지로 설정 화면이 열렸나
            public bool settingsEscClosed;        // 한 번 더 보내 닫혔나
            public bool musicDropReady;           // 끌어다 놓기를 받을 준비가 되었나
            public bool musicDropOk;              // 끌어다 놓은 wav가 들어가 분석되고 설정 화면의 음악 탭이 열렸나
            public int settingsClockSpeedBefore;  // 설정 화면을 열기 전의 진행 속도 (0이면 운영이 돌지 않았다)
            public bool settingsClockPaused;      // 설정 화면이 떠 있는 동안 진행 속도가 0이었나
            public bool settingsClockResumed;     // 닫은 뒤 전의 속도로 돌아왔나
            public string storePlace;             // 매장 운영 창: 연 매장
            public bool storeDeskOpen;            // 카페를 누른 길로 매장 창이 열렸나
            public bool storeOneAtATime;          // 동 관리 창이 닫히고 오른쪽 건물 카드도 뜨지 않았나
            public bool storeCoversMap;           // 창 위에서는 지도가 눌리지 않는가
            public string storeActionKey;         // 열 때의 할 일 (order · inspect · produce · 빈 값)
            public bool storeUnchangedOnOpen;     // 열고 탭 다섯을 본 것만으로 시계 · 원장 · 발주가 그대로인가
            public bool storeOrderTried;          // 본부 발주를 해야 해서 확정을 한 번 보냈나
            public int storeOrdersAdded;          // 그때 는 발주 수 (1이어야 한다)
            public int storeLedgerAdded;          // 그때 는 원장 줄 수 (본부 대금과 센터의 재고 예약)
            public int storePayableAdded;         // 그때 는 이 매장의 본부 대금 줄 수 (1이어야 한다)
            public string storeOrderNotice;       // 그때의 알림
            public bool storeBackToDong;          // 「← 동 관리」로 동 관리 창이 열리고 매장 창이 닫혔나
            public float closeMetresPerPixel;     // 가까이 보기: 배율 1에서 논리 픽셀 하나의 길이(m)
            public float closeOldLimitMetres;     // 전의 확대 한계(4배)에서 화면 가로에 담기던 땅(m)
            public float closeMaxZoom;            // 지금의 확대 한계
            public float closeLimitMetres;        // 지금의 한계에서 화면 가로에 담기는 땅(m)
            public float closePitchFar;           // 멀리(가로 1400 m)에서의 내려다보는 각
            public float closePitchMid;           // 중간(가로 560 m)
            public float closePitchNear;          // 가장 가까이
            public float closeNearMetres;         // 가장 가까이에서 화면 가로에 담긴 땅(m)
            public bool closeDeskOpen;            // 가장 가까이에서 매장 창이 떠 있었나
            public int closeRoofsSampled;         // 가장 가까이에서 센 옥상
            public int closeRoofsPicked;          // 그 가운데 눌러서 같은 건물이 골라진 수
            public float closePitchBack;          // 멀리로 돌아온 뒤의 각
            public bool closeUnchanged;           // 보는 것만으로 시계 · 원장이 그대로인가
            public float closeFrameMsFar;         // 멀리(가로 1400 m)에서 마흔 프레임의 평균 시간(ms)
            public float closeFrameMsNear;        // 가장 가까이에서 마흔 프레임의 평균 시간(ms)
            public int closeVSync;                // 그때의 수직 동기 설정(0이면 꺼짐)
            public int closeTargetFps;            // 그때의 목표 프레임(-1이면 정하지 않음)
            public float closeFrameMsFarFree;     // 수직 동기를 끄고: 멀리에서 백스무 프레임의 평균(ms)
            public float closeFrameMsFarWorst;    //   그 가운데 가장 긴 한 프레임(ms)
            public float closeFrameMsNearFree;    // 수직 동기를 끄고: 가장 가까이에서 평균(ms)
            public float closeFrameMsNearWorst;   //   그 가운데 가장 긴 한 프레임(ms)
            public bool closeFrameRestored;       // 잰 뒤 수직 동기 · 목표 프레임을 전의 값으로 되돌렸나
            public int lampCount;                 // 길가 불빛(표현): 놓은 자리의 수
            public int lampRoadCount;             // 그 가운데 큰길
            public int lampMapWidth;              // 빛 그림의 칸(가로)
            public int lampMapHeight;             // 빛 그림의 칸(세로)
            public float lampCellMetres;          // 한 칸(m)
            public float lampNear;                // 가장 가까이에서 셰이더에 넘긴 세기
            public float lampOff;                 // 껐을 때의 세기(0)
            public float lampFar;                 // 멀리로 돌아왔을 때의 세기(0이어야 전과 같다)
            public float lampMeanOn;              // 가장 가까이 그림의 평균 밝기: 켬
            public float lampMeanOff;             // 끔
            public float lampLitShare;            // 켠 그림이 끈 그림보다 뚜렷이 밝은 칸의 몫(0~1)
            public int rebakeRimVertices;         // 품5: 읽어 들인 동 메시의 지붕 테두리 정점
            public int rebakeShopWallVertices;    // 품5: 근린생활시설 벽 정점
            public int greenVertices;             // 계1: 녹지 정점(uv.x 1~4)
            public int streetSidewalkVertices;    // 보1: 보도 정점(uv.x 5)
            public int streetCrosswalkVertices;   // 거1: 횡단보도 줄 정점(6)
            public int streetTactileVertices;     //   점자블록(7)
            public int streetKerbLineVertices;    // 보1: 연석선(8)
            public int streetFurnitureVertices;   //   시설물대(9)
            public bool keyF1;                    // r219 단축키: F1로 도움말이 열림
            public bool keyF1Closed;              //   F1을 다시 누르면 닫힘
            public bool keyF2;                    //   F2로 동 관리 창
            public bool keyF3;                    //   F3으로 매장 창(요약)
            public bool keyF4;                    //   F4 재료 탭
            public bool keyF5;                    //   F5 설비 탭
            public bool keyF6;                    //   F6 인력 탭
            public bool keyF3Again;               //   닫았다가 F3이면 보던 탭
            public bool keyIgnoredInSettings;     //   설정 화면이 떠 있으면 받지 않음
            public bool keyUnchanged;             //   키를 눌러도 시계 · 원장 그대로
            public bool keyM;                     //   M으로 업무 요약
            public bool keyMBack;                 //   M을 다시 누르면 작은 지도
            public bool dayCardShown;             // 카1: 첫날 마감에 하루 마감 카드
            public int dayCardDay;
            public int dayCardCompleted;
            public int dayCardTotal;
            public int dayCardBalance;
            public int dayCardPlayerAssigns;
            public int dayCardAutopilotAssigns;
            public bool dayCardNextEnabled;
            public bool dayAdvanced;              //   카드의 「다음 영업 시작까지」로 다음 날이 열림
            public int dayAfterCard;
            public bool waitingSignalDangerInKernel;   //   커널에서는 배차 대기 위험
            public bool waitingSignalSoftShown;   //   맡김이 켜져 있으면 화면에서는 주의
            public string waitingSignalShownText;
            public bool autopilotDefaultOff;      // 켜 두기 성장 · 맡김: 촬영에서는 처음에 꺼져 있음
            public bool autopilotReachedPolicy;   //   경계를 넘겨 방침 국면에 닿음
            public int autopilotPolicies;         //   맡김이 이어 간 방침 수
            public int autopilotDayBefore;
            public int autopilotDayAfter;
            public int autopilotAssigned;         //   맡김이 배차한 주문 수
            public int autopilotChecks;           //   맡김이 한 출발 전 점검 수
            public int autopilotWaitingWork;      //   기다리는 일 수
            public bool autopilotReturnShown;     //   손이 돌아왔을 때 「다녀오신 사이」
            public string autopilotReturnText;
            public bool autopilotOffQuiet;        //   끈 뒤에는 명령 0개
            public int autopilotLedgerWhileOff;   //   (참고) 끈 동안 시계만 넘겼을 때 늘어난 원장 줄
            public string autopilotLog;
            public bool noticeDefaultOff;         // 떴다 사라지는 알림: 촬영에서는 처음에 「그대로」
            public int noticeDay;                 //   알림 장면을 시작한 날(앞 장면이 배차한 날을 넘김)
            public string noticeOrder;
            public bool noticeAssignShown;        //   배차하면 그 매장에 빛과 「기사 · 배차」
            public string noticeAssignText;
            public bool noticeDeliveredShown;     //   배달 완료가 배송지에 뜸
            public bool noticeSettleShown;        //   정산이 금액과 함께 뜸
            public long noticeSettleAmount;
            public string noticeSettleText;
            public int noticeSettleEngineDelta;   //   (참고) 같은 사이 엔진 요약에서 늘어난 가상 차액
            public int noticeWaitingInKernel;     //   (참고) 커널의 배차 대기 신호 수
            public int noticeWaitingInBar;        //   맡김이 켜져 있으면 위쪽 줄에는 0
            public bool noticeDayCloseShown;      //   하루 마감 알림
            public string noticeDayCloseText;
            public bool noticeDayCardHidden;      //   알림만 뜨고 카드는 닫혀 있음
            public bool noticeDayCardOnClick;     //   알림을 누르면 카드가 열림
            public bool noticeClosedChip;         //   카드를 닫으면 「n일차 영업 마감 · 카드 보기」 칩이 남음
            public bool noticeChipReopens;        //   칩을 누르면 카드가 다시 열림
            public bool mingdeDefaultUnbound;     // 명1: 게임에서는 명덕 자료 준비 전(빛 그대로)
            public int mingdeLights;              //   시험값을 넣은 빛 수(기사 둘 · 매장 둘)
            public float mingdeLowSize;           //   명덕 0의 크기 · 세기 배율
            public float mingdeLowStrength;
            public float mingdeHighSize;          //   명덕 240(시험값)의 크기 · 세기 배율
            public float mingdeHighStrength;
            public bool mingdeRises;              //   크기도 세기도 함께 커짐
            public bool mingdeClearedUnbound;     //   시험값을 거두면 다시 자료 준비 전
            public string pickerBefore;           // r236 행정동 이동 창: 열기 전 보던 동
            public bool pickerOpened;             //   M으로 열림
            public int pickerModules;             //   실행판이 읽는 모듈 수
            public int pickerCards;               //   그린 카드 수
            public int pickerThumbs;              //   그중 미리보기를 그린 카드(판본이 맞는 것)
            public string pickerTarget;           //   누른 동
            public bool pickerMoved;              //   창이 닫히고 그 동만 보기로
            public bool pickerTargetOnScreen;     //   그 동의 가운데가 지도 영역 안
            public string pickerAfter;            //   옮긴 뒤 사업 칸이 말하는 동
            public bool pickerSameAgain;          //   같은 동을 다시 눌러도 그대로
            public bool pickerEscClosed;          //   Esc는 창만 닫고 설정을 열지 않음
            public bool pickerUnchanged;          //   시계 · 원장 그대로
            public bool placeOpened;              // 홍달 플레이스 주문자 뷰: 열림
            public bool placeFoldedPanels;        //   경영 창들을 접음(지도만 보기 상태)
            public string placeKeyword;           //   고른 검색어
            public int placePlaces;               //   장소 표의 수(내 매장 + 시험 자료)
            public int placeSitesPlaced;          //   그중 자리를 정한 수
            public int placeCandidates;           //   검색에 맞는 후보 수
            public int placeExcluded;             //   제외(의도 불일치 · 자리 미확인)
            public int placePages;                //   쪽 수
            public int placeFirstPageCards;       //   1쪽 카드 수(투영)
            public int placeCardsDrawn;           //   그린 카드 수(스크롤 안에 보이는 것)
            public int placePinsDrawn;            //   지도 번호 핀 수
            public int placePinsClear;            //   그중 앱 판에 가리지 않은 핀(누를 수 있는 핀)
            public int placeMineRank;             //   내 매장의 순위 · 쪽(라떼)
            public int placeMinePage;
            public bool placeOrderMatches;        //   카드 순서 = 순위표 1쪽
            public bool placeCustomerFieldsOnly;  //   주문자 카드는 허용한 칸만
            public int placeSecondPageCards;      //   2쪽 카드 수
            public bool placePageKeepsOrder;      //   쪽을 넘겨도 순위표 그대로
            public bool placeDetailOpened;        //   내 매장 상세가 열림
            public int placeMineRankCafe;         //   「카페」에서 내 매장 순위
            public int placeCafeCandidates;
            public bool placeEscClosed;           //   Esc로 닫히고 지도만 보기 상태가 되돌아옴
            public bool placeUnchanged;           //   시계 · 원장 그대로
            public string phoneLevel;             // 홍달 기사 휴대폰: 찍을 때의 지도 단계
            public bool phoneShown;               //   배차 기사를 누르면 오른쪽에 휴대폰이 뜸
            public string phoneCourier;           //   누른 기사
            public string phoneRestScreen;        //   열 때의 화면(대기 · 요청 · 진행 · 완료)
            public bool phoneIdleOnOpen;          //   열 때는 재연하지 않음
            public int phoneReplayScenes;         //   배정 → 수락 대본의 장면 수
            public string phoneReplayTap;         //   손가락이 누른 단추
            public bool phoneReplayDrained;       //   재연이 끝나 큐가 빔
            public bool phoneCardToggle;          //   「카드」로 예전 기사 카드가 보임
            public bool phoneUnchanged;           //   시계 · 원장 · 주문 단계 그대로
            public bool cargoPhoneShown;          // 홍달 화물: 뜸
            public string cargoStage;             //   찍을 때의 운행 단계(화면 단계)
            public bool cargoIdleOnOpen;          //   열 때는 재연하지 않음
            public string cargoTap;               //   배차 추천에서 손가락이 누른 단추
            public bool centerPhoneShown;         // 홍달 창고: 뜸
            public string centerStage;            //   찍을 때의 출고 단계
            public string centerTap;              //   손가락이 누른 단추
            public bool storePhoneShown;          // 홍달 가게: 뜸
            public string storePush;              //   주문 수신 알림
            public int soakStartDay;              // 생명주기 확인(관찰용, 실패 판정 밖): 맡김으로 사흘을 굴림 (SagajeongDispatchSoakProof.cs)
            public int soakEndDay;
            public int soakDays;                  //   넘어간 날 수
            public int soakStuckDays;             //   마감 요약에 끝나지 않은 주문이 남은 날
            public string soakRows;               //   날마다의 마감 요약
            public int soakOrdersAdded;           //   Core 주문 수 증가
            public int soakCompletedAdded;        //   그중 정산까지 끝난 수
            public int soakBalanceAdded;          //   Core 가상 차액 증가
            public long soakFundBefore;           //   동 수요 자금(쓸 수 있는 돈)
            public long soakFundAfter;
            public long soakDemandRevenueAdded;   //   동 수요 집계 수입 증가
            public int soakOpenDongs;             //   열린 동
            public int soakLedgerAdded;           //   원장 줄 증가
            public int soakHeldBefore;            //   받을 사업이 없어 보류된 인계
            public int soakHeldAfter;
            public int soakAutopilotAssigns;      //   맡김의 배차
            public int soakMindBrokenMost;        //   무념이 깨진 기사(가장 많을 때)
            public int soakWaitingNow;            //   끝에서 기다리는 주문
            public bool workSummaryDrawn;         //   업무 요약을 그렸나
            public int iconCount;                 // 그1: 읽어 들인 아이콘 수(29)
            public int iconAtlasWidth;            // 아이콘 그림의 크기(512 × 256)
            public int iconAtlasHeight;
            public bool iconMissingRefused;       // 없는 이름은 그리지 않는가 (탭 아이콘 이름도 모두 있는가)
            public bool pictureDeskDefaultOff;    // 그2: 설정의 기본이 지금 창(글 중심)인가
            public bool pictureDeskDrawn;         // 켰을 때 매장 창 머리에 그림을 그렸나
            public bool pictureSummaryDrawn;      // 그3: 켰을 때 요약 탭에 흐름 줄 여섯 칸을 그렸나
            public bool pictureSummaryAfterOrder; // 그3: 발주 뒤에도 그렸나 (발주를 하지 않은 촬영이면 false 그대로)
            public bool pictureStockDrawn;        // 그3: 재료 탭에 재고 막대를 그렸나
            public bool miniShown;                // 요약 지도: 가까이 보기 · 매장 창과 함께 떴나
            public int miniDongs;                 // 그린 준비된 동의 수
            public int miniPlaces;                // 찍은 내 곳의 수
            public string miniDong;               // 지금 보는 곳이 든 동
            public string miniCafeDong;           // 카페 1호점이 든 동(건물 자료)
            public bool miniCoversMap;            // 요약 지도 위에서는 지도가 눌리지 않는가
            public bool miniSpotInside;           // 금색 점이 구 지도 칸 안에 있는가
            public float miniCentreOffMetres;     // 요약 지도가 읽은 보는 곳과 실제로 가운데 둔 곳의 차(m)
            public float miniViewBoxCells;        // 동 전체를 볼 때 보이는 땅 네모의 대각선(칸)
            public bool miniHomeMoved;            // 「내 매장으로」가 큰 지도를 옮겼나
            public float miniHomeOffMetres;       // 옮긴 뒤 그 매장과 보는 곳의 차(m)
            public bool miniHomeKeptZoom;         // 그때 배율은 그대로인가
            public bool miniFoldedLine;           // 접으면 한 줄만 남는가
            public bool miniGivesWay;             // 오른쪽에 건물 카드가 뜨면 자리를 내주는가
            public bool miniUnchanged;            // 그것만으로 시계 · 원장이 그대로인가
            public bool miniDongLevelShown;       // 머리의 「동」을 고르면 동 단계 그림이 뜨는가
            public string miniPick;               // 요약 지도에서 준비된 동의 한 곳(매장 A 자리)을 누른 결과 (moved여야 한다)
            public float miniPickOffMetres;       // 옮긴 뒤 그곳과 보는 곳의 차(m)
            public bool miniPickKeptZoom;         // 그때 배율은 그대로인가
            public string miniPickUnready;        // 준비 전인 곳을 누른 결과 (unready여야 한다)
            public bool miniPickUnreadyStayed;    // 그때 큰 지도는 그대로인가
            public bool musicUnchanged;           // 촬영 전후로 시계 · 원장 · 기억한 곡이 그대로인가
            public bool startedInStationView;     // 처음 보기가 역 중심인가
            public string startedStation;         // 처음에 본 역
        }

        [Serializable]
        private sealed class ViewProofShot
        {
            public string file;
            public float yaw;
            public float pitch;
            public float zoom;
            public bool dioramaVisible;
            public int roofsSampled;      // 옥상 가운데가 화면 안에 들어온 건물 가운데 센 수
            public int roofsPicked;       // 그 점을 눌렀을 때 같은 건물이 골라진 수
        }

        private bool TryStartViewProof(string[] args)
        {
            var option = args.FirstOrDefault(item => item.StartsWith(ViewProofFlag, StringComparison.Ordinal));
            if (option == null) return false;
            StartCoroutine(ViewProofWatchdog());
            StartCoroutine(CaptureViewProof(option.Substring(ViewProofFlag.Length)));
            return true;
        }

        // 촬영이 어디선가 멈춰도 창이 남지 않게 한다.
        private IEnumerator ViewProofWatchdog()
        {
            yield return new WaitForSecondsRealtime(ViewProofTimeoutSeconds);
            Debug.LogWarning("Hongdal view proof: timed out");
            Application.Quit(3);
        }

        private IEnumerator CaptureViewProof(string directory)
        {
            Directory.CreateDirectory(directory);
            var receipt = new ViewProofReceipt { screenWidth = Screen.width, screenHeight = Screen.height };
            var deadline = Time.realtimeSinceStartupAsDouble + 45;
            yield return Until(() => state != null && pending == null && DioramaVisible && DongModulesLoaded &&
                (dongSoloPrimed || !DongFlagsActive), deadline);
            if (error != null || state == null || !DioramaVisible)
            {
                receipt.failure = error ?? "2.5D 동네 지도가 열리지 않았습니다.";
                FinishViewProof(directory, receipt);
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.5f);
            receipt.startedInStationView = StationView;
            receipt.startedStation = StationView ? Station.Name : string.Empty;

            foreach (var (yaw, pitch, file) in ViewProofShots)
            {
                SetDioramaLens(yaw, pitch);
                ShowWholeDong();                 // 새 방향에서 한 동을 다시 화면에 맞춘다
                yield return null;
                yield return null;
                yield return null;
                var (sampled, picked) = ViewProofRoofPicks();
                receipt.shots.Add(new ViewProofShot
                {
                    file = file, yaw = (float)dioramaLens.YawDegrees, pitch = (float)dioramaLens.PitchDegrees, zoom = mapZoom,
                    dioramaVisible = DioramaVisible, roofsSampled = sampled, roofsPicked = picked,
                });
                yield return SaveFrame(directory, file);
            }

            // 기본 방향으로 돌아와 설정 칸(지도 방향 줄)을 연 모습
            SetDioramaLens(디오라마시점.DefaultYawDegrees, 디오라마시점.DefaultPitchDegrees);
            ShowWholeDong();
            settingsOpen = true;
            settingsTab = SettingsTabMap;
            yield return null;
            yield return null;
            yield return SaveFrame(directory, "07-settings.png");
            settingsOpen = false;
            yield return null;

            // r174 「오늘 결산」 칸을 열고, 확인을 눌러 본다. 읽음 표시만 바뀌어야 한다.
            dailyReportOpen = true;
            dailyReportCache = null;
            yield return null;
            yield return null;
            yield return SaveFrame(directory, "08-daily-report.png");
            var todayKey = TodayKey;
            var todayBefore = DailyReports().Days.FirstOrDefault(candidate => candidate.Report.DateKey == todayKey);
            var elapsedBefore = engine.Kernel.ElapsedMs;
            var ledgerBefore = engine.Kernel.Ledger.Count;
            receipt.dailyReportEntries = todayBefore == null ? 0 : todayBefore.Report.Entries;
            if (todayBefore != null) ConfirmDailyReport(todayKey);
            yield return null;
            yield return null;
            yield return SaveFrame(directory, "09-daily-report-confirmed.png");
            var todayAfter = DailyReports().Days.FirstOrDefault(candidate => candidate.Report.DateKey == todayKey);
            receipt.dailyReportConfirmed = todayAfter != null && todayAfter.Reading == 일일결산읽음.Read;
            receipt.dailyReportUnchanged = engine.Kernel.ElapsedMs == elapsedBefore && engine.Kernel.Ledger.Count == ledgerBefore;
            dailyReportOpen = false;
            yield return null;

            yield return ViewProofRounds(directory, receipt);
            yield return ViewProofManaged(directory, receipt);
            yield return ViewProofPerspective(directory, receipt);
            yield return ViewProofStation(directory, receipt);
            yield return ViewProofPastReports(directory, receipt);
            yield return ViewProofRoam(directory, receipt);
            yield return ViewProofDongDesk(directory, receipt);
            yield return ViewProofCrowd(directory, receipt);
            yield return ViewProofMusic(directory, receipt);
            yield return ViewProofStoreDesk(directory, receipt);
            yield return ViewProofShortcuts(directory, receipt);
            yield return ViewProofCloseView(directory, receipt);
            yield return ViewProofMiniMap(directory, receipt);
            yield return ViewProofIcons(directory, receipt);
            yield return ViewProofFirstDay(directory, receipt);      // 카1 첫날 끝내기 (SagajeongDispatchDayClose.cs)
            yield return ViewProofAutopilot(directory, receipt);     // 켜 두기 성장 · 맡김 (SagajeongDispatchAutopilot.cs)
            yield return ViewProofNotices(directory, receipt);       // 떴다 사라지는 알림 (SagajeongDispatchNotices.cs)
            yield return ViewProofMingde(directory, receipt);        // 명1 명덕 → 빛 (SagajeongDispatchMingdeLight.cs)
            yield return ViewProofDongPicker(directory, receipt);    // r236 M 행정동 이동 창 (SagajeongDispatchDongPicker.cs)
            yield return ViewProofPlace(directory, receipt);         // 홍달 플레이스 주문자 뷰 (SagajeongDispatchPlace.cs)
            yield return ViewProofRiderPhone(directory, receipt);    // 홍달 기사 휴대폰 재연 (SagajeongDispatchRiderPhone.cs)
            yield return ViewProofSoak(directory, receipt);          // 생명주기 확인: 맡김으로 사흘 (SagajeongDispatchSoakProof.cs, 관찰용)

            // 기본 방향에서의 비율을 기준으로, 다른 방향에서도 옥상 가운데가 같은 건물로 골라지는지 본다.
            var first = receipt.shots[0];
            var baseline = first.roofsSampled == 0 ? 0f : first.roofsPicked / (float)first.roofsSampled;
            receipt.passed = first.roofsSampled >= 20 && baseline >= 0.5f && receipt.shots.All(shot =>
                shot.dioramaVisible && shot.roofsSampled >= 20 && shot.roofsPicked / (float)shot.roofsSampled >= baseline - 0.2f);
            if (!receipt.passed) receipt.failure = "방향을 바꾼 화면에서 옥상 가운데와 건물 고르기가 기본 방향만큼 맞지 않습니다.";
            else if (receipt.dailyReportEntries > 0 && !(receipt.dailyReportConfirmed && receipt.dailyReportUnchanged))
            {
                receipt.passed = false;
                receipt.failure = "오늘 결산 확인이 읽음 표시만 바꾸지 않았습니다.";
            }
            else if (!(receipt.roundOneBound && receipt.roundBlockedWhileRunning && receipt.roundCloseUnchanged &&
                receipt.roundTwoDifferent && receipt.roundWorldKept && receipt.roundsClosed == 2))
            {
                receipt.passed = false;
                receipt.failure = "운영 회차 두 번이 기대대로 이어지지 않았습니다.";
            }
            else if (!(receipt.managedRoleBuildings >= 1 && receipt.managedPartnersAdded > 0 &&
                receipt.managedPartnerSitesAfter - receipt.managedPartnerSites == receipt.managedPartnersAdded))
            {
                receipt.passed = false;
                receipt.failure = "제휴를 맺은 만큼 관리 건물이 더 켜지지 않았습니다.";
            }
            else if (!(receipt.perspectiveUnchanged && receipt.perspectiveRoofsSampled >= 20 &&
                receipt.perspectiveRoofsPicked / (float)receipt.perspectiveRoofsSampled >= baseline - 0.2f))
            {
                receipt.passed = false;
                receipt.failure = "원근 보기에서 건물 고르기가 맞지 않거나, 켜고 끈 뒤 업무 상태가 달라졌습니다.";
            }
            else if (!(receipt.startedInStationView && receipt.stationUnchanged && receipt.stationDongs >= 2 && receipt.stationRoofsSampled >= 20 &&
                receipt.stationRoofsPicked / (float)receipt.stationRoofsSampled >= baseline - 0.2f))
            {
                receipt.passed = false;
                receipt.failure = "처음 보기가 역 중심이 아니거나, 역 중심 보기에서 건물 고르기가 맞지 않거나, 보기를 바꾼 뒤 업무 상태가 달라졌습니다.";
            }
            else if (!(receipt.pastReportsBefore == 0 && receipt.pastReportsPending == 1 && receipt.pastReportsAfterConfirm == 0 &&
                receipt.pastReportUnchanged))
            {
                receipt.passed = false;
                receipt.failure = "지난 결산 안내가 기대대로 뜨고 사라지지 않았습니다.";
            }
            else if (!(receipt.roamRiders > 0 && receipt.roamGraphNodes > 0 && receipt.roamMetres > 100f && receipt.roamInsideBuildings == 0 &&
                receipt.roamUnchanged && receipt.roamLitCells > 0 && receipt.roamLightKinds >= 2 && receipt.roamRidersMore > receipt.roamRiders &&
                receipt.roamLitCellsMore > receipt.roamLitCells && receipt.roamRidersAfter == receipt.roamRiders && receipt.lightPlaces > 0 &&
                receipt.lightCard && receipt.lightMenuRows >= 2 && !string.IsNullOrEmpty(receipt.businessDong) &&
                receipt.lightBeatStep > 0.3f))
            {
                receipt.passed = false;
                receipt.failure = "기사가 길 그래프 위를 돌아다니지 않았거나, 건물 안에 들어갔거나, 업무 상태가 달라졌거나, 빛이 켜지지 않았거나, 기사가 늘어도 더 밝아지지 않았습니다.";
            }
            else if (!(receipt.deskOpen && !string.IsNullOrEmpty(receipt.deskDong) && receipt.deskUnchanged &&
                receipt.deskContractAdded == (receipt.deskContractTried ? 1 : 0) && receipt.deskContractSpent == receipt.deskContractCost))
            {
                receipt.passed = false;
                receipt.failure = "동 관리 창이 열리지 않았거나, 여는 것만으로 업무 상태가 달라졌거나, 창의 건설이 제휴 하나와 그 값만큼만 바꾸지 않았습니다.";
            }
            else if (!(receipt.crowdUnchanged && receipt.crowdNeighbours >= 3 && receipt.crowdCoresOnTop > 0 && receipt.crowdDimMin < 0.95f &&
                receipt.crowdPairs > 0 && receipt.crowdPhaseAfter > receipt.crowdPhaseBefore + 0.1f))
            {
                receipt.passed = false;
                receipt.failure = "빛이 몰린 곳에서 가운데 점이 맨 위에 그려지지 않았거나, 연한 부분이 옅어지지 않았거나, 이웃한 빛의 숨이 엇갈리지 않았거나, 업무 상태가 달라졌습니다.";
            }
            else if (!(receipt.musicFound && receipt.musicWavOk && receipt.musicMp3Ok && receipt.musicPulsePeak > 0.4f &&
                receipt.musicPulseRest < -0.5f && receipt.musicUnchanged && receipt.musicDropOk &&
                (receipt.settingsClockSpeedBefore == 0 || receipt.settingsClockPaused && receipt.settingsClockResumed)))
            {
                receipt.passed = false;
                receipt.failure = "시험 곡(wav · mp3)에서 박을 찾지 못했거나, 빛이 박에 맞춰 퍼졌다 움츠러들지 않았거나, 끌어다 놓은 곡이 들어가지 않았거나, 설정 화면이 게임 시간을 멈췄다 잇지 않았거나, 업무 상태 · 기억한 곡이 달라졌습니다.";
            }
            else if (!(receipt.storeDeskOpen && receipt.storeOneAtATime && receipt.storeCoversMap && receipt.storeUnchangedOnOpen &&
                receipt.storeBackToDong && (!receipt.storeOrderTried || receipt.storeOrdersAdded == 1 && receipt.storePayableAdded == 1)))
            {
                receipt.passed = false;
                receipt.failure = "카페를 눌러 매장 운영 창이 열리지 않았거나, 동 관리 창과 함께 떴거나, 여는 것만으로 업무가 바뀌었거나, 창의 본부 발주가 꼭 한 번만 잡히지 않았거나, 「← 동 관리」로 돌아가지 않았습니다.";
            }
            else if (!(receipt.lampCount > 0 && receipt.lampNear > 0.5f && receipt.lampOff == 0f && receipt.lampFar == 0f &&
                receipt.lampLitShare > 0.002f && receipt.rebakeRimVertices > 0 && receipt.rebakeShopWallVertices > 0))
            {
                receipt.passed = false;
                receipt.failure = "길가 불빛이 놓이지 않았거나, 가장 가까이에서 켜지지 않았거나, 끄거나 멀리 갔을 때 꺼지지 않았거나, 켠 그림에서 밝아진 곳이 거의 없거나, 다시 구운 동 메시의 지붕 테두리 · 가게 벽이 읽히지 않았습니다.";
            }
            else if (!(receipt.greenVertices > 0 && receipt.streetSidewalkVertices > 0 && receipt.streetCrosswalkVertices > 0 &&
                receipt.streetTactileVertices > 0 && receipt.streetKerbLineVertices > 0 && receipt.streetFurnitureVertices > 0))
            {
                receipt.passed = false;
                receipt.failure = "다시 구운 동 메시에서 녹지나 거리 겹(보도 · 횡단보도 · 점자블록 · 연석선 · 시설물대)이 읽히지 않았습니다.";
            }
            else if (!(Mathf.Abs(receipt.closeLimitMetres - CloseViewWidthMetres) < 5f && Mathf.Abs(receipt.closePitchFar - receipt.closePitchBack) < 0.01f &&
                Mathf.Abs(receipt.closePitchNear - (float)ClosePitchDegrees) < 0.5f && receipt.closePitchMid < receipt.closePitchFar - 1f &&
                receipt.closePitchMid > receipt.closePitchNear + 1f && receipt.closeRoofsSampled >= 5 &&
                receipt.closeRoofsPicked >= receipt.closeRoofsSampled * 0.5f && receipt.closeUnchanged && receipt.closeFrameRestored &&
                receipt.closeFrameMsFarFree > 0f && receipt.closeFrameMsNearFree > 0f))
            {
                receipt.passed = false;
                receipt.failure = "가까이 보기에서 화면 가로가 200 m에 닿지 않았거나, 가까워질수록 각이 눕지 않았거나, 멀리로 돌아와 전의 각이 되지 않았거나, 옥상을 눌러 그 건물이 골라지지 않았거나, 보는 것만으로 업무가 바뀌었습니다.";
            }
            else if (!(receipt.miniShown && receipt.miniDongs > 0 && receipt.miniPlaces > 0 && receipt.miniDong == receipt.miniCafeDong &&
                receipt.miniCoversMap && receipt.miniSpotInside && receipt.miniCentreOffMetres < 3f && receipt.miniViewBoxCells >= MiniViewBoxFloor &&
                receipt.miniHomeMoved && receipt.miniHomeOffMetres < 3f && receipt.miniHomeKeptZoom && receipt.miniFoldedLine &&
                receipt.miniGivesWay && receipt.miniUnchanged && receipt.miniDongLevelShown && receipt.miniPick == "moved" &&
                receipt.miniPickOffMetres < 3f && receipt.miniPickKeptZoom && receipt.miniPickUnready == "unready" && receipt.miniPickUnreadyStayed))
            {
                receipt.passed = false;
                receipt.failure = "요약 지도가 뜨지 않았거나, 보는 곳의 동 · 자리가 맞지 않았거나, 요약 지도 위에서 지도가 눌렸거나, 내 매장으로 · 접기 · 자리 내주기 · 동 단계 · 눌러서 옮기기가 되지 않았거나, 그것만으로 업무가 바뀌었습니다.";
            }
            else if (!(receipt.iconCount == IconNames.Length && receipt.iconAtlasWidth == IconColumns * IconCell &&
                receipt.iconAtlasHeight == IconRows * IconCell && receipt.iconMissingRefused && receipt.pictureDeskDefaultOff && receipt.pictureDeskDrawn &&
                receipt.pictureSummaryDrawn && receipt.pictureStockDrawn && (!receipt.storeOrderTried || receipt.pictureSummaryAfterOrder)))
            {
                receipt.passed = false;
                receipt.failure = "아이콘 한 벌(그1)을 읽지 못했거나, 그림 크기가 512 × 256이 아니거나, 없는 이름의 아이콘을 그렸거나, 왼쪽 창 그림 중심(그2)이 기본으로 켜져 있거나 켰을 때 머리 그림을 그리지 않았습니다.";
            }
            else if (!(receipt.keyF1 && receipt.keyF1Closed && receipt.keyF2 && receipt.keyF3 && receipt.keyF4 && receipt.keyF5 && receipt.keyF6 &&
                receipt.keyF3Again && receipt.keyIgnoredInSettings && receipt.keyUnchanged && receipt.keyM && receipt.keyMBack && receipt.workSummaryDrawn))
            {
                receipt.passed = false;
                receipt.failure = "단축키(r219): F1~F6이 창 · 탭을 열지 않았거나, F3이 보던 탭으로 다시 열지 않았거나, M이 작은 지도와 업무 요약을 바꾸지 않았거나, 설정 화면에서도 받았거나, 키로 업무가 바뀌었습니다.";
            }
            else if (!(receipt.autopilotDefaultOff && receipt.autopilotReachedPolicy && receipt.autopilotPolicies >= 1 &&
                receipt.autopilotDayAfter > receipt.autopilotDayBefore && receipt.autopilotAssigned >= 1 && receipt.autopilotReturnShown &&
                receipt.autopilotOffQuiet))
            {
                receipt.passed = false;
                receipt.failure = "맡김(켜 두기 성장 뼈대): 촬영에서 처음부터 켜져 있었거나, 손이 빈 동안 방침을 이어 가 다음 날로 넘어가지 않았거나, " +
                    "기다린 주문을 배차하지 않았거나, 「다녀오신 사이」가 뜨지 않았거나, 끈 뒤에도 명령을 보냈습니다.";
            }
            else if (!(receipt.dayCardShown && receipt.dayCardTotal > 0 && receipt.dayCardNextEnabled && receipt.dayAdvanced &&
                receipt.waitingSignalDangerInKernel && receipt.waitingSignalSoftShown))
            {
                receipt.passed = false;
                receipt.failure = "카1 첫날: 영업 마감에 하루 마감 카드가 뜨지 않았거나, 카드의 「다음 영업 시작까지」로 다음 날이 열리지 않았거나, " +
                    "맡김이 켜진 동안 배차 대기 위험이 화면에서 주의로 낮아지지 않았습니다.";
            }
            else if (!(receipt.noticeDefaultOff && receipt.noticeAssignShown && receipt.noticeDeliveredShown && receipt.noticeSettleShown &&
                receipt.noticeSettleAmount > 0 && receipt.noticeWaitingInBar == 0 && receipt.noticeDayCloseShown && receipt.noticeDayCardHidden &&
                receipt.noticeDayCardOnClick && receipt.noticeClosedChip && receipt.noticeChipReopens))
            {
                receipt.passed = false;
                receipt.failure = "떴다 사라지는 알림: 배차 · 배달 완료 · 정산 금액 알림이 뜨지 않았거나, 맡김이 켜진 동안 배차 대기가 위쪽 줄에 남았거나, " +
                    "하루 마감 카드가 알림 없이 바로 떴거나 알림을 눌러도 열리지 않았습니다.";
            }
            else if (!(receipt.mingdeDefaultUnbound && receipt.mingdeLights >= 2 && receipt.mingdeRises && receipt.mingdeClearedUnbound))
            {
                receipt.passed = false;
                receipt.failure = "명1 명덕 → 빛: 게임에서 명덕 자료 없이 빛이 바뀌었거나, 시험값에서 크기와 세기가 함께 커지지 않았거나, 시험값이 남았습니다.";
            }
            else if (!(receipt.pickerOpened && receipt.pickerCards == receipt.pickerModules && receipt.pickerCards > 0 &&
                receipt.pickerThumbs == receipt.pickerCards && receipt.pickerMoved && receipt.pickerTargetOnScreen && receipt.pickerSameAgain &&
                receipt.pickerEscClosed && receipt.pickerUnchanged))
            {
                receipt.passed = false;
                receipt.failure = "r236 행정동 이동 창: M으로 열리지 않았거나, 카드 · 미리보기가 모듈 수와 다르거나, 카드를 눌러 그 동만 보기로 옮기지 않았거나, " +
                    "같은 동을 다시 누를 때 · Esc로 닫을 때 기대와 달랐거나, 업무 상태가 바뀌었습니다.";
            }
            else if (!(receipt.placeOpened && receipt.placeFoldedPanels && receipt.placeSitesPlaced == receipt.placePlaces && receipt.placePlaces > 0 &&
                receipt.placeCandidates > 10 && receipt.placePages == 2 && receipt.placeFirstPageCards == 10 && receipt.placeCardsDrawn > 0 &&
                receipt.placePinsDrawn > 0 && receipt.placePinsClear == receipt.placeFirstPageCards && receipt.placeMineRank > 0 && receipt.placeOrderMatches && receipt.placeCustomerFieldsOnly &&
                receipt.placeSecondPageCards == receipt.placeCandidates - 10 && receipt.placePageKeepsOrder && receipt.placeDetailOpened &&
                receipt.placeMineRankCafe > 0 && receipt.placeEscClosed && receipt.placeUnchanged))
            {
                receipt.passed = false;
                receipt.failure = "홍달 플레이스 주문자 뷰: 열리지 않았거나, 자리를 정하지 못한 곳이 있거나, 쪽 · 카드 · 핀이 순위표와 다르거나, " +
                    "주문자 카드에 허용하지 않은 칸이 있거나, 내 매장 상세가 열리지 않았거나, Esc로 닫을 때 기대와 달랐거나, 업무 상태가 바뀌었습니다.";
            }
            else if (!(receipt.phoneShown && receipt.phoneIdleOnOpen && receipt.phoneReplayScenes == 4 && receipt.phoneReplayTap == "수락" &&
                receipt.phoneReplayDrained && receipt.phoneCardToggle && receipt.phoneUnchanged &&
                receipt.cargoPhoneShown && receipt.cargoIdleOnOpen && receipt.cargoTap == "추천 수락" &&
                receipt.centerPhoneShown && receipt.centerTap == "피킹 작업 시작" &&
                receipt.storePhoneShown && !string.IsNullOrEmpty(receipt.storePush) && receipt.storePush.EndsWith("주문을 수신했습니다.")))
            {
                receipt.passed = false;
                receipt.failure = "역할 휴대폰: 배차 기사를 눌러도 뜨지 않았거나, 열 때 재연했거나, 배정 → 수락 재연에서 「수락」을 누르지 않았거나, " +
                    "재연이 끝나지 않았거나, 「카드」로 바꾸지 못했거나, 홍달 화물 · 홍달 창고 · 홍달 가게가 뜨지 않았거나 기대한 단추 · 알림이 아니었거나, 업무 상태가 바뀌었습니다.";
            }
            FinishViewProof(directory, receipt);
        }

        // r187: 같은 세션에서 서로 다른 대상으로 운영 회차 두 번을 잇는다. 화면의 단추가 부르는 것(PressRound)을 그대로 부른다.
        // 시계는 촬영이 직접 넘긴다(작업이 진행되는 정상 경과). 마감과 다음 준비의 전후는 값으로 비교한다.
        private IEnumerator ViewProofRounds(string directory, ViewProofReceipt receipt)
        {
            const string riderA = "management-driver-jungnang-a";
            var deadline = Time.realtimeSinceStartupAsDouble + 30;
            holdProofClock = true;
            var ledgerAtStart = engine.Kernel.Ledger.Count;
            yield return null;
            yield return SaveFrame(directory, "10-round-1-prepare.png");

            PressRound(RoundTargetStatus);                       // 「운영 시작」
            yield return null;
            SelectCourier(MotorcycleRiderId);                    // 1회차: 기사 B → 매장 A의 주문
            ConnectStore("jungnang-a");
            yield return Until(() => pending == null && !AssignmentPending, deadline);
            actorFocus = null;
            yield return null;
            yield return null;
            receipt.roundOneBound = operatingRound.Phase == 회차국면.수행 && operatingRound.TargetId != null;
            var firstTarget = operatingRound.TargetId;
            PressRound(RoundTargetStatus);                       // 진행 중: 눌러도 마감되지 않아야 한다
            receipt.roundBlockedWhileRunning = operatingRound.Phase == 회차국면.수행 && operatingRound.Closed.Count == 0;
            yield return SaveFrame(directory, "11-round-1-running.png");

            for (var step = 0; step < 24 && RoundTargetStatus != 대상상태.전달 && error == null; step++)
            {
                Send(new DispatchRequest { action = "tick", elapsedMs = state.elapsedMs + 300_000 });
                yield return Until(() => pending == null, deadline);
                yield return null;
            }
            yield return null;
            yield return SaveFrame(directory, "12-round-1-delivered.png");

            var elapsedBefore = state.elapsedMs;
            var kernelElapsedBefore = engine.Kernel.ElapsedMs;
            var ledgerBefore = engine.Kernel.Ledger.Count;
            var kernelBefore = engine.Kernel.Save();
            PressRound(RoundTargetStatus);                       // 「회차 마감」
            yield return null;
            yield return SaveFrame(directory, "13-round-1-result.png");
            var closedOnce = operatingRound.Phase == 회차국면.결과 && operatingRound.Closed.Count == 1;
            PressRound(RoundTargetStatus);                       // 「다음 운영 준비」
            yield return null;
            receipt.roundCloseUnchanged = closedOnce && operatingRound.Number == 2 && operatingRound.Phase == 회차국면.준비 &&
                state.elapsedMs == elapsedBefore && engine.Kernel.ElapsedMs == kernelElapsedBefore &&
                engine.Kernel.Ledger.Count == ledgerBefore && engine.Kernel.Save() == kernelBefore;

            SelectCourier(riderA);                               // 2회차: 기사 A → 매장 B의 주문
            ConnectStore("jungnang-b");
            yield return Until(() => pending == null && !AssignmentPending, deadline);
            actorFocus = null;
            yield return null;
            yield return null;
            receipt.roundTwoDifferent = operatingRound.Phase == 회차국면.수행 && operatingRound.TargetId != null &&
                operatingRound.TargetId != firstTarget;
            for (var step = 0; step < 24 && RoundTargetStatus != 대상상태.전달 && error == null; step++)
            {
                Send(new DispatchRequest { action = "tick", elapsedMs = state.elapsedMs + 300_000 });
                yield return Until(() => pending == null, deadline);
                yield return null;
            }
            PressRound(RoundTargetStatus);                       // 「회차 마감」
            yield return null;
            yield return SaveFrame(directory, "14-round-2-result.png");
            receipt.roundsClosed = operatingRound.Closed.Count;
            receipt.roundTargets = string.Join(", ", operatingRound.Closed.Select(record => record.Number + ":" + record.TargetId));
            receipt.roundWorldKept = engine.Kernel.Ledger.Count >= ledgerAtStart && error == null;
            holdProofClock = false;
        }

        // r143 첫 걸음: 회색 동네와 관리 건물. 끈 모습, 켠 모습, 제휴를 더 맺은 모습, 가까이 본 모습을 찍는다.
        // 제휴는 촬영 세션(저장하지 않는다)의 가상 자금으로 맺는다. 맺은 만큼 건물이 더 켜지는지 값으로도 본다.
        private IEnumerator ViewProofManaged(string directory, ViewProofReceipt receipt)
        {
            SetDioramaLens(디오라마시점.DefaultYawDegrees, 디오라마시점.DefaultPitchDegrees);
            actorFocus = null;
            selectedBuilding = null;
            ShowWholeDong();
            managedLight = false;
            yield return null;
            yield return null;
            yield return null;
            yield return SaveFrame(directory, "15-managed-off.png");

            managedLight = true;
            yield return null;
            yield return null;
            yield return null;
            receipt.managedRoleBuildings = managedRoleSites.Count;
            receipt.managedPartnerSites = managedPartnerSites.Count;
            yield return SaveFrame(directory, "16-managed-on.png");

            var region = soloRegion ?? focusDongRegion;
            var code = DongCodeOf(region);
            var partnersBefore = DongTrade(region)?.Partners ?? 0;
            for (var i = 0; i < 6 && code != null; i++) SendDong(BusinessIds.DongDemand, DongDemandActions.Contract, code);
            yield return null;
            yield return null;
            yield return null;
            receipt.managedPartnersAdded = (DongTrade(region)?.Partners ?? 0) - partnersBefore;
            receipt.managedPartnerSitesAfter = managedPartnerSites.Count;
            yield return SaveFrame(directory, "17-managed-more-partners.png");

            // 가까이: 매장들이 있는 곳을 세 배로 당겨 본다.
            if (state?.stores == null || !state.stores.Any()) yield break;
            ViewProofCentre(state.stores.Average(store => store.latitude), state.stores.Average(store => store.longitude), mapZoom * 3f);
            yield return null;
            yield return null;
            yield return null;
            yield return SaveFrame(directory, "18-managed-close.png");
            ShowWholeDong();
        }

        // 3D 기반 A6: 원근 보기. 켠 모습을 찍고, 옥상 가운데를 눌러 같은 건물이 골라지는지 세고,
        // 켜고 끈 전후로 시계·원장·커널 저장이 같은지 본다. 시계는 붙잡아 둔다.
        private IEnumerator ViewProofPerspective(string directory, ViewProofReceipt receipt)
        {
            holdProofClock = true;
            actorFocus = null;
            selectedBuilding = null;
            dailyReportOpen = false;
            settingsOpen = false;
            SetDioramaLens(디오라마시점.DefaultYawDegrees, 디오라마시점.DefaultPitchDegrees);
            ShowWholeDong();
            yield return null;
            var elapsedBefore = engine.Kernel.ElapsedMs;
            var ledgerBefore = engine.Kernel.Ledger.Count;
            var kernelBefore = engine.Kernel.Save();

            SetDioramaPerspective(true);
            yield return null;
            yield return null;
            yield return null;
            var (sampled, picked) = ViewProofRoofPicks();
            receipt.perspectiveRoofsSampled = sampled;
            receipt.perspectiveRoofsPicked = picked;
            yield return SaveFrame(directory, "19-perspective.png");

            settingsOpen = true;
            settingsTab = SettingsTabMap;
            yield return null;
            yield return null;
            yield return SaveFrame(directory, "20-perspective-settings.png");
            settingsOpen = false;

            if (state?.stores != null && state.stores.Any())
            {
                ViewProofCentre(state.stores.Average(store => store.latitude), state.stores.Average(store => store.longitude), mapZoom * 3f);
                yield return null;
                yield return null;
                yield return null;
                yield return SaveFrame(directory, "21-perspective-close.png");
            }

            SetDioramaLens(210, 40);
            ShowWholeDong();
            yield return null;
            yield return null;
            yield return null;
            yield return SaveFrame(directory, "22-perspective-turned-low.png");

            SetDioramaPerspective(false);
            SetDioramaLens(디오라마시점.DefaultYawDegrees, 디오라마시점.DefaultPitchDegrees);
            ShowWholeDong();
            yield return null;
            receipt.perspectiveUnchanged = engine.Kernel.ElapsedMs == elapsedBefore && engine.Kernel.Ledger.Count == ledgerBefore &&
                engine.Kernel.Save() == kernelBefore;
            holdProofClock = false;
        }

        // 역 중심 보기(옵션): 켠 모습, 다른 동의 사업 칸, 원근, 다음 역, 설정 칸을 찍는다.
        // 판 안에서 옥상 가운데를 눌러 같은 건물이 골라지는지 세고, 켜고 끈 전후로 시계·원장·커널 저장이 같은지 본다.
        private IEnumerator ViewProofStation(string directory, ViewProofReceipt receipt)
        {
            holdProofClock = true;
            actorFocus = null;
            selectedBuilding = null;
            dailyReportOpen = false;
            settingsOpen = false;
            SetDioramaLens(디오라마시점.DefaultYawDegrees, 디오라마시점.DefaultPitchDegrees);
            yield return null;
            var elapsedBefore = engine.Kernel.ElapsedMs;
            var ledgerBefore = engine.Kernel.Ledger.Count;
            var kernelBefore = engine.Kernel.Save();

            SetDongView(DongView.Station);
            yield return null;
            yield return null;
            yield return null;
            var (sampled, picked) = ViewProofRoofPicks();
            receipt.stationRoofsSampled = sampled;
            receipt.stationRoofsPicked = picked;
            receipt.stationDongs = stationAnchors.Count;
            receipt.stationCoverage = (float)stationCoverage;
            yield return SaveFrame(directory, "23-station.png");

            var other = stationAnchors.Keys.FirstOrDefault(key => key != stationCardRegion);
            if (other != null)
            {
                SelectStationDong(other);
                yield return null;
                yield return null;
                yield return SaveFrame(directory, "24-station-other-dong.png");
            }

            SetDioramaPerspective(true);
            yield return null;
            yield return null;
            yield return null;
            yield return SaveFrame(directory, "25-station-perspective.png");
            SetDioramaPerspective(false);

            StepStation(1);
            yield return null;
            yield return null;
            yield return null;
            receipt.stationNextCoverage = (float)stationCoverage;
            yield return SaveFrame(directory, "26-station-next.png");
            StepStation(-1);

            settingsOpen = true;
            settingsTab = SettingsTabMap;
            yield return null;
            yield return null;
            yield return SaveFrame(directory, "27-station-settings.png");
            settingsOpen = false;

            // 「이 동만」 보기는 설정 칸에 그대로 있다. 그 모습도 한 장 남긴다.
            SetDongView(DongView.Solo);
            yield return null;
            yield return null;
            yield return null;
            yield return SaveFrame(directory, "28-dong-solo-option.png");
            receipt.stationUnchanged = engine.Kernel.ElapsedMs == elapsedBefore && engine.Kernel.Ledger.Count == ledgerBefore &&
                engine.Kernel.Save() == kernelBefore;
            holdProofClock = false;
        }

        // r174 3절: 날짜가 지난 뒤의 안내. 촬영 세션(저장하지 않는다)에서 어제 날짜로 기록 한 줄을 만들고(제휴 한 곳, 가상 자금),
        // 「오늘 결산」 단추에 점이 뜨는지, 지난 날을 확인하면 사라지는지, 확인이 시계와 원장을 바꾸지 않는지 본다.
        private IEnumerator ViewProofPastReports(string directory, ViewProofReceipt receipt)
        {
            const long dayMs = 24L * 60 * 60 * 1000;
            var code = DongCodeOf(stationCardRegion ?? soloRegion ?? focusDongRegion);
            if (code == null) yield break;
            holdProofClock = true;
            actorFocus = null;
            selectedBuilding = null;
            settingsOpen = false;
            dailyReportOpen = false;
            receipt.pastReportsBefore = DailyPendingPast(true);

            engine.ShiftProofRealTime(-dayMs);
            SendDong(BusinessIds.DongDemand, DongDemandActions.Contract, code);
            engine.ShiftProofRealTime(0);
            yield return null;
            yield return null;
            receipt.pastReportsPending = DailyPendingPast(true);
            yield return SaveFrame(directory, "29-past-report-dot.png");

            dailyReportOpen = true;
            dailyReportCache = null;
            yield return null;
            yield return null;
            yield return SaveFrame(directory, "30-past-report-panel.png");

            var yesterday = 일일결산Policy.DateKey(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - dayMs);
            var elapsedBefore = engine.Kernel.ElapsedMs;
            var ledgerBefore = engine.Kernel.Ledger.Count;
            ConfirmDailyReport(yesterday);
            yield return null;
            yield return null;
            yield return SaveFrame(directory, "31-past-report-confirmed.png");
            dailyReportOpen = false;
            receipt.pastReportsAfterConfirm = DailyPendingPast(true);
            receipt.pastReportUnchanged = engine.Kernel.ElapsedMs == elapsedBefore && engine.Kernel.Ledger.Count == ledgerBefore;
            holdProofClock = false;
        }

        // 그 지점을 화면(가려지지 않는 부분)의 가운데에 두고 배율을 정한다. 촬영에서만 쓴다.
        private void ViewProofCentre(double latitude, double longitude, float zoom)
        {
            if (metresPerPixel <= 0) return;
            mapZoom = Mathf.Clamp(zoom, DongMinZoom, MaxMapZoom);
            SyncCloseLens();
            var metres = (float)metresPerPixel / mapZoom;
            var east = (float)((longitude - centerLongitude) * longitudeScale);
            var north = (float)((latitude - centerLatitude) * 111200);
            var centre = Field.size * 0.5f;
            mapOffset = new Vector2(centre.x * (1f - mapZoom) - east / metres, centre.y * (1f - mapZoom) + north / metres);
            mapOffset += FlatDeltaFor(dioramaOn, DongFitArea.center - Field.position - centre);
            mapOffset = ClampDioramaOffset(ClampMapOffset(mapOffset, mapZoom), mapZoom);
            localTextureDirty = true;
        }

        // 화면 안에 보이는 건물을 일곱 채에 하나씩 골라, 옥상 가운데를 눌렀을 때 그 건물이 골라지는지 센다.
        private (int Sampled, int Picked) ViewProofRoofPicks()
        {
            LoadBuildingIndex();
            var area = new Rect(Vector2.zero, Field.size);
            var sampled = 0;
            var picked = 0;
            for (var index = 0; index < buildingShapes.Length; index += 7)
            {
                var shape = buildingShapes[index];
                if (shape.Record.fill) continue;
                var top = RoofCentre(shape);
                if (!area.Contains(top)) continue;
                sampled++;
                if (PickBuildingInScene(top) == index) picked++;
            }
            return (sampled, picked);
        }

        private static void FinishViewProof(string directory, ViewProofReceipt receipt)
        {
            File.WriteAllText(Path.Combine(directory, "view-proof.json"), JsonUtility.ToJson(receipt, true));
            Debug.Log("Hongdal view proof: " + (receipt.passed ? "passed" : "failed: " + receipt.failure));
            Application.Quit(receipt.passed ? 0 : 2);
        }
    }
}
