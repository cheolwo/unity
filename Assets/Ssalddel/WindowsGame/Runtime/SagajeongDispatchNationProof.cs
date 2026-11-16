using System.Collections;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 전국 지도 촬영 (2026-11-16, 전국 물류시설 빛 시안의 바탕). 113 · 114는 해상 · 항공 층을 끈 모습. 관찰용이며 실패 판정에 넣지 않는다.
    //  - 지금 판의 전국 지도를 처음 보기와 수도권 확대(최대 배율)로 찍고, 세계 → 화면 맞춤 값을 받은 값에 적는다.
    //    시안은 이 값으로 공공 시설 좌표(경위도 → 팩의 세계 좌표 → 화면)를 정확히 얹는다.
    //  - 지도 높이만 바꾼다. 업무 · 시계 · 저장은 그대로(시계는 촬영이 잡아 둔다). 끝나면 동네 지도로 돌아온다.
    public sealed partial class SagajeongDispatchView
    {
        private IEnumerator ViewProofNationLights(string directory, ViewProofReceipt receipt)
        {
            holdProofClock = true;
            settingsOpen = false;
            dailyReportOpen = false;
            helpOpen = false;
            dongDeskOpen = false;
            dongPickerOpen = false;
            storeDeskPlace = null;
            actorFocus = null;
            selectedBuilding = null;
            showDesk = false;
            mapOnly = false;
            LoadMapLevel(MapLevel.Nation);
            for (var frame = 0; frame < 6; frame++) yield return null;
            receipt.nationShown = NationMapVisible;
            if (!receipt.nationShown)
            {
                holdProofClock = false;
                yield break;
            }
            receipt.nationFieldX = Field.x;
            receipt.nationFieldY = Field.y;
            receipt.nationOriginX = nationOrigin.x;
            receipt.nationOriginY = nationOrigin.y;
            receipt.nationScale = nationScale;
            yield return SaveFrame(directory, "111-nation.png");

            // 수도권: 서울을 가운데 두고 가장 크게
            var seoul = new Vector2(nationSpace.seoulX, nationSpace.seoulY);
            ZoomNation(NationMaxScale, NationPoint(seoul));
            var centre = new Vector2(Field.width * 0.5f, Field.height * 0.5f);
            nationOrigin += centre - NationPoint(seoul);
            ClampNation();
            for (var frame = 0; frame < 4; frame++) yield return null;
            receipt.nationZoomOriginX = nationOrigin.x;
            receipt.nationZoomOriginY = nationOrigin.y;
            receipt.nationZoomScale = nationScale;
            yield return SaveFrame(directory, "112-nation-capital.png");

            // 흐름을 끈 모습(물류 시설 빛 시안의 바탕): 해상 · 항공 층을 잠시 끄고 같은 자리와 전국 처음 보기를 찍는다. 찍고 나면 되돌린다.
            var seaWas = nationSeaOn;
            var airWas = nationAirOn;
            nationSeaOn = false;
            nationAirOn = false;
            for (var frame = 0; frame < 4; frame++) yield return null;
            yield return SaveFrame(directory, "113-nation-capital-quiet.png");
            nationManual = false;
            for (var frame = 0; frame < 6; frame++) yield return null;
            yield return SaveFrame(directory, "114-nation-quiet.png");
            nationSeaOn = seaWas;
            nationAirOn = airWas;

            ReturnToDong();
            for (var frame = 0; frame < 4; frame++) yield return null;
            receipt.nationBack = mapLevel == MapLevel.Dong;
            holdProofClock = false;
        }
    }
}
