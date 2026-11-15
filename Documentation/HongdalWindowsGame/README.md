# 홍달 경영 시뮬레이션 Windows 소스

게임의 커널·모듈, 2.5D 지도와 역할 휴대폰 화면, Windows 빌드 진입점은 [WindowsGame](../../Assets/Ssalddel/WindowsGame/)에 있다. 이 소스에는 가상 우유 공급 사슬의 설정과 두 디오라마 셰이더가 들어 있다. 지도 자료의 수집·검사·굽기에 쓰는 작성자 도구는 [Tools~](../../Assets/Ssalddel/WindowsGame/Tools~/)에 있다.

## 개발 환경

- Unity 6000.5.6f1과 .NET 10 SDK를 사용한다.
- Unity의 로컬 패키지들은 [Packages/manifest.json](../../Packages/manifest.json)에 지정된 경로로 연결된다. Unity 체크아웃을 `<작업폴더>/ssalddel`에 둔다면 Mirror 체크아웃은 `<작업폴더>/source/repos/Hongdal`에 둔다. 함께 쓰는 [배달 Core](https://github.com/cheolwo/mirror/tree/cbf7adfdc95930f2a80930774eb8c54d801fcfe0/Ssalddel.Sagajeong.Core/UnityPackage)의 기준 커밋은 `cbf7adfdc95930f2a80930774eb8c54d801fcfe0`이다.
- 내부 Windows 검토판은 별도로 준비한 `Resources/HongdalWindows/delivery-sample`, `road-graph`, 행정동 메시, 건물·지역 팩, 시험용 장소 목록과 이미지도 읽는다. 이 데이터는 내부 검토 또는 배포 조건 확인 대상이다. 공개 체크아웃으로 내부판의 지도, 장소 검색, 촬영 장면을 그대로 재현하려면 해당 데이터가 별도로 필요하다.

## 소스만으로 실행하는 검사

저장소 루트에서 실행한다. 여섯 프로젝트 모두 콘솔 프로그램이며 종료 코드와 통과 건수를 출력한다.

```powershell
Get-ChildItem 'Assets/Ssalddel/WindowsGame/Checks~' -Recurse -Filter '*.csproj' |
    ForEach-Object {
        dotnet run --project $_.FullName
        if ($LASTEXITCODE -ne 0) { throw "검사 실패 $($_.FullName)" }
    }
```

개별 검사 대상은 구 지도 이름 배치, 결정 순회, 행정동 모듈, 새 주문 표시, 전국 흐름, 기사 무념이다. 원본 데이터를 준비한 작업실에서는 Unity Editor의 `Ssalddel.WindowsGame.Editor.WindowsGameBuild.Build`로 빈 출력 폴더에 Windows 검토판을 만든다. 내부판의 빌드·자동 촬영 결과는 당시 자료 판본에 귀속된다.
