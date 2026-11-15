using Newtonsoft.Json;

namespace Ssalddel.WindowsGame.Kernel
{
    /// <summary>
    /// 업종 팩 1판: 첫 인계 사슬(r71 "우유 5팩")에 쓰는 가상 자료.
    /// 모듈은 숫자를 코드에 적지 않고 이 팩에서 읽는다. 식별자는 ASCII만 쓴다(큰 Simulation의 Stable ID 규칙).
    /// </summary>
    public sealed class FirstChainPack
    {
        public string schema = string.Empty;
        public string id = string.Empty;
        public string version = string.Empty;
        public PackNetwork network = new PackNetwork();
        public PackItem item = new PackItem();
        public PackCenter center = new PackCenter();
        public PackTransport transport = new PackTransport();
        public PackInspection inspection = new PackInspection();
        public PackRecipe recipe = new PackRecipe();
        public int recommendedOrderQuantity = 5;
        public int waitingCups = 1;

        public static FirstChainPack Parse(string json)
        {
            var pack = JsonConvert.DeserializeObject<FirstChainPack>(json);
            if (pack == null || string.IsNullOrEmpty(pack.id) || pack.item == null || pack.item.contentQuantity <= 0)
                throw new JsonException("업종 팩을 읽을 수 없습니다.");
            return pack;
        }
    }

    public sealed class PackNetwork
    {
        public string id = string.Empty;
        public PackParty headquarters = new PackParty();
        public PackParty supplier = new PackParty();
        public PackStore store = new PackStore();
    }

    public class PackParty
    {
        public string id = string.Empty;
        public string code = string.Empty;
        public string name = string.Empty;
    }

    public sealed class PackStore : PackParty
    {
        public string placeId = string.Empty;
        public string membershipId = string.Empty;
    }

    public sealed class PackItem
    {
        public string productId = string.Empty;
        public string name = string.Empty;
        public string orderUnit = string.Empty;
        public decimal contentQuantity;
        public string contentUnit = string.Empty;
        public string storage = string.Empty;
        public decimal supplyPrice;
        public decimal storePrice;
        public decimal minimum = 1m;
        public decimal maximum = 1m;
    }

    public sealed class PackCenter
    {
        public string id = string.Empty;
        public string name = string.Empty;
        public PackLot[] lots = new PackLot[0];
        public int pickSeconds;
        public int loadSeconds;
    }

    public sealed class PackLot
    {
        public string id = string.Empty;
        public int available;
    }

    public sealed class PackTransport
    {
        public string vehicleId = string.Empty;
        public string vehicleName = string.Empty;
        public string vehicleKind = string.Empty;
        public int travelSeconds;
    }

    public sealed class PackInspection
    {
        public decimal suggestAccept;
        public decimal suggestQuarantine;
        public string reason = string.Empty;
    }

    public sealed class PackRecipe
    {
        public string menu = string.Empty;
        public decimal contentPerCup;
        public decimal price;
    }
}
