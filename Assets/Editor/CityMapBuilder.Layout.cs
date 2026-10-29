// 자동 생성 파일 (tools/layout.py) - 4배 도시 맵의 배치 데이터. 직접 고치기보다 숫자만 바꿔 쓰세요.
using UnityEngine;

public static partial class CityMapBuilder
{
    private const float DeckY = 6f;

    private static void BuildGeneratedFloors(Transform g)
    {
        Box("Base", g, new Vector3(2f, -0.5f, 212f), new Vector3(172f, 1f, 488f), "Asphalt", true);
        Box("SafeRoomFloor", g, new Vector3(0f, 0.01f, 1f), new Vector3(8f, 0.01f, 10f), "Tile", false);
        Box("MidRoomFloor", g, new Vector3(49f, 0.01f, 193f), new Vector3(10f, 0.01f, 10f), "Tile", false);
        Box("ParkGrass", g, new Vector3(42f, 0.01f, 149f), new Vector3(44f, 0.01f, 78f), "Grass", false);
        Box("ParkGateFloor", g, new Vector3(14f, 0.01f, 130f), new Vector3(12f, 0.01f, 16f), "Concrete", false);
        Box("TowerFloor", g, new Vector3(41f, 0.01f, 222f), new Vector3(34f, 0.01f, 44f), "Concrete", false);
        Box("PlazaFloor", g, new Vector3(0f, 0.01f, 402f), new Vector3(80f, 0.01f, 60f), "Tile", false);
        Box("AlleyFloor", g, new Vector3(0f, 0.01f, 32f), new Vector3(40f, 0.01f, 52f), "Concrete", false);
        Box("ParkPath", g, new Vector3(42f, 0.01f, 134f), new Vector3(44f, 0.02f, 4f), "Concrete", false);
        Box("ParkPath", g, new Vector3(42f, 0.01f, 149f), new Vector3(4f, 0.02f, 78f), "Concrete", false);
        Box("ParkPath", g, new Vector3(49f, 0.01f, 185f), new Vector3(10f, 0.02f, 6f), "Concrete", false);
        Box("Sidewalk", g, new Vector3(-6.5f, 0.075f, 75f), new Vector3(3f, 0.15f, 34f), "Concrete", true);
        Box("Sidewalk", g, new Vector3(-6.5f, 0.075f, 127f), new Vector3(3f, 0.15f, 46f), "Concrete", true);
        Box("Sidewalk", g, new Vector3(6.5f, 0.075f, 75f), new Vector3(3f, 0.15f, 34f), "Concrete", true);
        Box("Sidewalk", g, new Vector3(6.5f, 0.075f, 113f), new Vector3(3f, 0.15f, 18f), "Concrete", true);
        Box("Sidewalk", g, new Vector3(6.5f, 0.075f, 144f), new Vector3(3f, 0.15f, 12f), "Concrete", true);
        Box("LaneLine", g, new Vector3(0f, 0.012f, 60f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(0f, 0.012f, 66f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(0f, 0.012f, 72f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(0f, 0.012f, 78f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(0f, 0.012f, 84f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(0f, 0.012f, 90f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(0f, 0.012f, 108f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(0f, 0.012f, 114f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(0f, 0.012f, 120f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(0f, 0.012f, 126f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(0f, 0.012f, 132f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(0f, 0.012f, 138f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(0f, 0.012f, 144f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(0f, 0.012f, 150f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(0f, 0.012f, 156f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(0f, 0.012f, 162f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(0f, 0.012f, 168f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("Crosswalk", g, new Vector3(-3.6f, 0.012f, 90.5f), new Vector3(0.6f, 0.01f, 2.4f), "LineWhite", false);
        Box("Crosswalk", g, new Vector3(-3.6f, 0.012f, 105.5f), new Vector3(0.6f, 0.01f, 2.4f), "LineWhite", false);
        Box("Crosswalk", g, new Vector3(-2.4f, 0.012f, 90.5f), new Vector3(0.6f, 0.01f, 2.4f), "LineWhite", false);
        Box("Crosswalk", g, new Vector3(-2.4f, 0.012f, 105.5f), new Vector3(0.6f, 0.01f, 2.4f), "LineWhite", false);
        Box("Crosswalk", g, new Vector3(-1.2f, 0.012f, 90.5f), new Vector3(0.6f, 0.01f, 2.4f), "LineWhite", false);
        Box("Crosswalk", g, new Vector3(-1.2f, 0.012f, 105.5f), new Vector3(0.6f, 0.01f, 2.4f), "LineWhite", false);
        Box("Crosswalk", g, new Vector3(0f, 0.012f, 90.5f), new Vector3(0.6f, 0.01f, 2.4f), "LineWhite", false);
        Box("Crosswalk", g, new Vector3(0f, 0.012f, 105.5f), new Vector3(0.6f, 0.01f, 2.4f), "LineWhite", false);
        Box("Crosswalk", g, new Vector3(1.2f, 0.012f, 90.5f), new Vector3(0.6f, 0.01f, 2.4f), "LineWhite", false);
        Box("Crosswalk", g, new Vector3(1.2f, 0.012f, 105.5f), new Vector3(0.6f, 0.01f, 2.4f), "LineWhite", false);
        Box("Crosswalk", g, new Vector3(2.4f, 0.012f, 90.5f), new Vector3(0.6f, 0.01f, 2.4f), "LineWhite", false);
        Box("Crosswalk", g, new Vector3(2.4f, 0.012f, 105.5f), new Vector3(0.6f, 0.01f, 2.4f), "LineWhite", false);
        Box("Crosswalk", g, new Vector3(3.6f, 0.012f, 90.5f), new Vector3(0.6f, 0.01f, 2.4f), "LineWhite", false);
        Box("Crosswalk", g, new Vector3(3.6f, 0.012f, 105.5f), new Vector3(0.6f, 0.01f, 2.4f), "LineWhite", false);
        Box("ParkingLine", g, new Vector3(30.5f, 0.012f, 208.5f), new Vector3(5f, 0.01f, 0.12f), "LineWhite", false);
        Box("ParkingLine", g, new Vector3(50.5f, 0.012f, 208.5f), new Vector3(5f, 0.01f, 0.12f), "LineWhite", false);
        Box("ParkingLine", g, new Vector3(30.5f, 0.012f, 211.5f), new Vector3(5f, 0.01f, 0.12f), "LineWhite", false);
        Box("ParkingLine", g, new Vector3(50.5f, 0.012f, 211.5f), new Vector3(5f, 0.01f, 0.12f), "LineWhite", false);
        Box("ParkingLine", g, new Vector3(30.5f, 0.012f, 214.5f), new Vector3(5f, 0.01f, 0.12f), "LineWhite", false);
        Box("ParkingLine", g, new Vector3(50.5f, 0.012f, 214.5f), new Vector3(5f, 0.01f, 0.12f), "LineWhite", false);
        Box("ParkingLine", g, new Vector3(30.5f, 0.012f, 217.5f), new Vector3(5f, 0.01f, 0.12f), "LineWhite", false);
        Box("ParkingLine", g, new Vector3(50.5f, 0.012f, 217.5f), new Vector3(5f, 0.01f, 0.12f), "LineWhite", false);
        Box("ParkingLine", g, new Vector3(30.5f, 0.012f, 220.5f), new Vector3(5f, 0.01f, 0.12f), "LineWhite", false);
        Box("ParkingLine", g, new Vector3(50.5f, 0.012f, 220.5f), new Vector3(5f, 0.01f, 0.12f), "LineWhite", false);
        Box("ParkingLine", g, new Vector3(30.5f, 0.012f, 223.5f), new Vector3(5f, 0.01f, 0.12f), "LineWhite", false);
        Box("ParkingLine", g, new Vector3(50.5f, 0.012f, 223.5f), new Vector3(5f, 0.01f, 0.12f), "LineWhite", false);
        Box("ParkingLine", g, new Vector3(30.5f, 0.012f, 226.5f), new Vector3(5f, 0.01f, 0.12f), "LineWhite", false);
        Box("ParkingLine", g, new Vector3(50.5f, 0.012f, 226.5f), new Vector3(5f, 0.01f, 0.12f), "LineWhite", false);
        Box("ParkingLine", g, new Vector3(30.5f, 0.012f, 229.5f), new Vector3(5f, 0.01f, 0.12f), "LineWhite", false);
        Box("ParkingLine", g, new Vector3(50.5f, 0.012f, 229.5f), new Vector3(5f, 0.01f, 0.12f), "LineWhite", false);
        Box("ParkingLine", g, new Vector3(30.5f, 0.012f, 232.5f), new Vector3(5f, 0.01f, 0.12f), "LineWhite", false);
        Box("ParkingLine", g, new Vector3(50.5f, 0.012f, 232.5f), new Vector3(5f, 0.01f, 0.12f), "LineWhite", false);
        Box("ParkingLine", g, new Vector3(30.5f, 0.012f, 235.5f), new Vector3(5f, 0.01f, 0.12f), "LineWhite", false);
        Box("ParkingLine", g, new Vector3(50.5f, 0.012f, 235.5f), new Vector3(5f, 0.01f, 0.12f), "LineWhite", false);
        Box("ParkingLine", g, new Vector3(30.5f, 0.012f, 238.5f), new Vector3(5f, 0.01f, 0.12f), "LineWhite", false);
        Box("ParkingLine", g, new Vector3(50.5f, 0.012f, 238.5f), new Vector3(5f, 0.01f, 0.12f), "LineWhite", false);
        Box("ParkingLine", g, new Vector3(30.5f, 0.012f, 241.5f), new Vector3(5f, 0.01f, 0.12f), "LineWhite", false);
        Box("ParkingLine", g, new Vector3(50.5f, 0.012f, 241.5f), new Vector3(5f, 0.01f, 0.12f), "LineWhite", false);
    }

    // 보이지 않는 벽 (땅: 걸을 수 있는 곳의 테두리, 고가 아래는 낮게 / 고가: 가장자리)
    private static void BuildGeneratedWalls(Transform g)
    {
        Wall(g, -40.2f, 0f, 402f, 0.4f, 40f, 60f);
        Wall(g, -20.2f, 0f, 356f, 0.4f, 40f, 32f);
        Wall(g, -18.2f, 0f, 98f, 0.4f, 40f, 12f);
        Wall(g, -16.2f, 0f, 22f, 0.4f, 40f, 24f);
        Wall(g, -16.2f, 0f, 46f, 0.4f, 40f, 16f);
        Wall(g, -11.8f, 0f, 16f, 0.4f, 40f, 4f);
        Wall(g, -11.8f, 0f, 26f, 0.4f, 40f, 8f);
        Wall(g, -11.8f, 0f, 46f, 0.4f, 40f, 8f);
        Wall(g, -8.2f, 0f, 26f, 0.4f, 40f, 8f);
        Wall(g, -8.2f, 0f, 36f, 0.4f, 40f, 4f);
        Wall(g, -8.2f, 0f, 75f, 0.4f, 40f, 34f);
        Wall(g, -8.2f, 0f, 127f, 0.4f, 40f, 46f);
        Wall(g, -4.2f, 0f, 3f, 0.4f, 40f, 14f);
        Wall(g, -3.8f, 0f, 22f, 0.4f, 40f, 8f);
        Wall(g, -3.8f, 0f, 34f, 0.4f, 40f, 8f);
        Wall(g, -4.2f, 0f, 46f, 0.4f, 40f, 8f);
        Wall(g, -4.2f, 0f, 56f, 0.4f, 40f, 4f);
        Wall(g, -3.8f, 0f, 356f, 0.4f, 40f, 32f);
        Wall(g, 0.2f, 0f, 44f, 0.4f, 40f, 12f);
        Wall(g, 4.2f, 0f, 3f, 0.4f, 40f, 14f);
        Wall(g, 3.8f, 0f, 22f, 0.4f, 40f, 8f);
        Wall(g, 3.8f, 0f, 36f, 0.4f, 40f, 12f);
        Wall(g, 4.2f, 0f, 56f, 0.4f, 40f, 4f);
        Wall(g, 8.2f, 0f, 30f, 0.4f, 40f, 16f);
        Wall(g, 8.2f, 0f, 75f, 0.4f, 40f, 34f);
        Wall(g, 8.2f, 0f, 113f, 0.4f, 40f, 18f);
        Wall(g, 8.2f, 0f, 144f, 0.4f, 40f, 12f);
        Wall(g, 11.8f, 0f, 16f, 0.4f, 40f, 4f);
        Wall(g, 11.8f, 0f, 28f, 0.4f, 40f, 12f);
        Wall(g, 11.8f, 0f, 46f, 0.4f, 40f, 8f);
        Wall(g, 16.2f, 0f, 22f, 0.4f, 40f, 24f);
        Wall(g, 16.2f, 0f, 46f, 0.4f, 40f, 16f);
        Wall(g, 18.2f, 0f, 98f, 0.4f, 40f, 12f);
        Wall(g, 19.8f, 0f, 116f, 0.4f, 40f, 12f);
        Wall(g, 19.8f, 0f, 163f, 0.4f, 40f, 50f);
        Wall(g, 23.8f, 0f, 218f, 0.4f, 40f, 36f);
        Wall(g, 23.8f, 0f, 240f, 0.4f, 5.4f, 8f);
        Wall(g, 40.2f, 0f, 402f, 0.4f, 40f, 60f);
        Wall(g, 43.8f, 0f, 193f, 0.4f, 40f, 10f);
        Wall(g, 46.8f, 0f, 199f, 0.4f, 40f, 2f);
        Wall(g, 51.2f, 0f, 199f, 0.4f, 40f, 2f);
        Wall(g, 54.2f, 0f, 193f, 0.4f, 40f, 10f);
        Wall(g, 58.2f, 0f, 241f, 0.4f, 5.4f, 6f);
        Wall(g, 64.2f, 0f, 149f, 0.4f, 40f, 78f);
        Wall(g, 64.2f, 0f, 219f, 0.4f, 40f, 38f);
        Wall(g, 0f, 0f, -4.2f, 8f, 40f, 0.4f);
        Wall(g, -10f, 0f, 9.8f, 12f, 40f, 0.4f);
        Wall(g, 10f, 0f, 9.8f, 12f, 40f, 0.4f);
        Wall(g, 0f, 0f, 14.2f, 24f, 40f, 0.4f);
        Wall(g, -8f, 0f, 17.8f, 8f, 40f, 0.4f);
        Wall(g, 8f, 0f, 17.8f, 8f, 40f, 0.4f);
        Wall(g, -10f, 0f, 22.2f, 4f, 40f, 0.4f);
        Wall(g, 10f, 0f, 22.2f, 4f, 40f, 0.4f);
        Wall(g, 0f, 0f, 25.8f, 8f, 40f, 0.4f);
        Wall(g, -10f, 0f, 29.8f, 4f, 40f, 0.4f);
        Wall(g, 0f, 0f, 30.2f, 8f, 40f, 0.4f);
        Wall(g, -12f, 0f, 34.2f, 8f, 40f, 0.4f);
        Wall(g, 14f, 0f, 34.2f, 4f, 40f, 0.4f);
        Wall(g, -12f, 0f, 37.8f, 8f, 40f, 0.4f);
        Wall(g, -2f, 0f, 37.8f, 4f, 40f, 0.4f);
        Wall(g, 12f, 0f, 37.8f, 8f, 40f, 0.4f);
        Wall(g, -8f, 0f, 42.2f, 8f, 40f, 0.4f);
        Wall(g, 8f, 0f, 42.2f, 8f, 40f, 0.4f);
        Wall(g, -8f, 0f, 49.8f, 8f, 40f, 0.4f);
        Wall(g, 6f, 0f, 49.8f, 12f, 40f, 0.4f);
        Wall(g, -10f, 0f, 54.2f, 12f, 40f, 0.4f);
        Wall(g, 10f, 0f, 54.2f, 12f, 40f, 0.4f);
        Wall(g, -6f, 0f, 57.8f, 4f, 40f, 0.4f);
        Wall(g, 6f, 0f, 57.8f, 4f, 40f, 0.4f);
        Wall(g, -13f, 0f, 91.8f, 10f, 40f, 0.4f);
        Wall(g, 13f, 0f, 91.8f, 10f, 40f, 0.4f);
        Wall(g, -13f, 0f, 104.2f, 10f, 40f, 0.4f);
        Wall(g, 13f, 0f, 104.2f, 10f, 40f, 0.4f);
        Wall(g, 42f, 0f, 109.8f, 44f, 40f, 0.4f);
        Wall(g, 14f, 0f, 121.8f, 12f, 40f, 0.4f);
        Wall(g, 14f, 0f, 138.2f, 12f, 40f, 0.4f);
        Wall(g, 0f, 0f, 150.2f, 16f, 40f, 0.4f);
        Wall(g, 32f, 0f, 188.2f, 24f, 40f, 0.4f);
        Wall(g, 59f, 0f, 188.2f, 10f, 40f, 0.4f);
        Wall(g, 45.5f, 0f, 198.2f, 3f, 40f, 0.4f);
        Wall(g, 52.5f, 0f, 198.2f, 3f, 40f, 0.4f);
        Wall(g, 35.5f, 0f, 199.8f, 23f, 40f, 0.4f);
        Wall(g, 57.5f, 0f, 199.8f, 13f, 40f, 0.4f);
        Wall(g, 61f, 0f, 238.2f, 6f, 5.4f, 0.4f);
        Wall(g, 41f, 0f, 244.2f, 34f, 40f, 0.4f);
        Wall(g, -12f, 0f, 339.8f, 16f, 5.4f, 0.4f);
        Wall(g, -30f, 0f, 371.8f, 20f, 40f, 0.4f);
        Wall(g, 18f, 0f, 371.8f, 44f, 40f, 0.4f);
        Wall(g, 0f, 0f, 432.2f, 80f, 40f, 0.4f);
        Wall(g, -20.2f, 6f, 286f, 0.4f, 34f, 172f);
        Wall(g, -3.8f, 6f, 218f, 0.4f, 34f, 36f);
        Wall(g, -3.8f, 6f, 308f, 0.4f, 34f, 128f);
        Wall(g, 23.8f, 6f, 218f, 0.4f, 34f, 36f);
        Wall(g, 58.2f, 6f, 203f, 0.4f, 34f, 6f);
        Wall(g, 64.2f, 6f, 225f, 0.4f, 34f, 38f);
        Wall(g, -12f, 6f, 199.8f, 16f, 34f, 0.4f);
        Wall(g, 41f, 6f, 199.8f, 34f, 34f, 0.4f);
        Wall(g, 61f, 6f, 205.8f, 6f, 34f, 0.4f);
        Wall(g, 10f, 6f, 235.8f, 28f, 34f, 0.4f);
        Wall(g, 30f, 6f, 244.2f, 68f, 34f, 0.4f);
        Wall(g, -12f, 6f, 372.2f, 16f, 34f, 0.4f);
    }

    // 고가 / 옥상 / 다리 / 경사로
    private static void BuildGeneratedDecks(Transform g)
    {
        Deck(g, "roof", 24f, 58f, 200f, 244f, "Concrete");
        Deck(g, "landing", 58f, 64f, 238f, 244f, "Concrete");
        Deck(g, "bridge", -4f, 24f, 236f, 244f, "Asphalt");
        Deck(g, "highway", -20f, -4f, 200f, 340f, "Asphalt");
        Ramp(g, 58f, 64f, 206f, 238f, 0f, 6f, true);
        Ramp(g, -20f, -4f, 372f, 340f, 0f, 6f, true);
        Box("GuardRail", g, new Vector3(-19.85f, 6.5f, 270f), new Vector3(0.3f, 1f, 140f), "Concrete", true);
        Box("GuardRail", g, new Vector3(-4.15f, 6.5f, 218f), new Vector3(0.3f, 1f, 36f), "Concrete", true);
        Box("GuardRail", g, new Vector3(-4.15f, 6.5f, 292f), new Vector3(0.3f, 1f, 96f), "Concrete", true);
        Box("GuardRail", g, new Vector3(24.15f, 6.5f, 218f), new Vector3(0.3f, 1f, 36f), "Concrete", true);
        Box("GuardRail", g, new Vector3(57.85f, 6.5f, 203f), new Vector3(0.3f, 1f, 6f), "Concrete", true);
        Box("GuardRail", g, new Vector3(63.85f, 6.5f, 241f), new Vector3(0.3f, 1f, 6f), "Concrete", true);
        Box("GuardRail", g, new Vector3(-12f, 6.5f, 200.15f), new Vector3(16f, 1f, 0.3f), "Concrete", true);
        Box("GuardRail", g, new Vector3(41f, 6.5f, 200.15f), new Vector3(34f, 1f, 0.3f), "Concrete", true);
        Box("GuardRail", g, new Vector3(10f, 6.5f, 236.15f), new Vector3(28f, 1f, 0.3f), "Concrete", true);
        Box("GuardRail", g, new Vector3(30f, 6.5f, 243.85f), new Vector3(68f, 1f, 0.3f), "Concrete", true);
        Box("LaneLine", g, new Vector3(-12f, DeckY + 0.012f, 204f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(-12f, DeckY + 0.012f, 210f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(-12f, DeckY + 0.012f, 216f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(-12f, DeckY + 0.012f, 222f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(-12f, DeckY + 0.012f, 228f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(-12f, DeckY + 0.012f, 234f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(-12f, DeckY + 0.012f, 240f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(-12f, DeckY + 0.012f, 246f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(-12f, DeckY + 0.012f, 252f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(-12f, DeckY + 0.012f, 258f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(-12f, DeckY + 0.012f, 264f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(-12f, DeckY + 0.012f, 270f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(-12f, DeckY + 0.012f, 276f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(-12f, DeckY + 0.012f, 282f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(-12f, DeckY + 0.012f, 288f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(-12f, DeckY + 0.012f, 294f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(-12f, DeckY + 0.012f, 300f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(-12f, DeckY + 0.012f, 306f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(-12f, DeckY + 0.012f, 312f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(-12f, DeckY + 0.012f, 318f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(-12f, DeckY + 0.012f, 324f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
        Box("LaneLine", g, new Vector3(-12f, DeckY + 0.012f, 330f), new Vector3(0.18f, 0.01f, 3f), "LineWhite", false);
    }

    // 건물: 길 테두리를 따라 자동으로 (깊이가 얕은 곳은 벽돌 벽)
    private static void BuildGeneratedBuildings(Transform g)
    {
        BuildingRow(g, new Vector3(-40f, 0f, 432.45f), new Vector3(1f, 0f, 0f), 16f, new Vector3(0f, 0f, -1f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(-24f, 0f, 432.45f), new Vector3(1f, 0f, 0f), 16f, new Vector3(0f, 0f, -1f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(-8f, 0f, 432.45f), new Vector3(1f, 0f, 0f), 16f, new Vector3(0f, 0f, -1f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(8f, 0f, 432.45f), new Vector3(1f, 0f, 0f), 16f, new Vector3(0f, 0f, -1f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(24f, 0f, 432.45f), new Vector3(1f, 0f, 0f), 16f, new Vector3(0f, 0f, -1f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(64.45f, 0f, 110f), new Vector3(0f, 0f, 1f), 16f, new Vector3(-1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(64.45f, 0f, 126f), new Vector3(0f, 0f, 1f), 15f, new Vector3(-1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(64.45f, 0f, 141f), new Vector3(0f, 0f, 1f), 16f, new Vector3(-1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(64.45f, 0f, 157f), new Vector3(0f, 0f, 1f), 15f, new Vector3(-1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(64.45f, 0f, 172f), new Vector3(0f, 0f, 1f), 16f, new Vector3(-1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(-40.45f, 0f, 372f), new Vector3(0f, 0f, 1f), 15f, new Vector3(1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(-40.45f, 0f, 387f), new Vector3(0f, 0f, 1f), 15f, new Vector3(1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(-40.45f, 0f, 402f), new Vector3(0f, 0f, 1f), 15f, new Vector3(1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(-40.45f, 0f, 417f), new Vector3(0f, 0f, 1f), 15f, new Vector3(1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(40.45f, 0f, 372f), new Vector3(0f, 0f, 1f), 15f, new Vector3(-1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(40.45f, 0f, 387f), new Vector3(0f, 0f, 1f), 15f, new Vector3(-1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(40.45f, 0f, 402f), new Vector3(0f, 0f, 1f), 15f, new Vector3(-1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(40.45f, 0f, 417f), new Vector3(0f, 0f, 1f), 15f, new Vector3(-1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(19.55f, 0f, 138f), new Vector3(0f, 0f, 1f), 12f, new Vector3(1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(19.55f, 0f, 150f), new Vector3(0f, 0f, 1f), 13f, new Vector3(1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(19.55f, 0f, 163f), new Vector3(0f, 0f, 1f), 13f, new Vector3(1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(19.55f, 0f, 176f), new Vector3(0f, 0f, 1f), 12f, new Vector3(1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(-8.45f, 0f, 104f), new Vector3(0f, 0f, 1f), 15f, new Vector3(1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(-8.45f, 0f, 119f), new Vector3(0f, 0f, 1f), 16f, new Vector3(1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(-8.45f, 0f, 135f), new Vector3(0f, 0f, 1f), 15f, new Vector3(1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(20f, 0f, 109.55f), new Vector3(1f, 0f, 0f), 15f, new Vector3(0f, 0f, 1f), 5.55f, LowBuildings, true);
        BuildingRow(g, new Vector3(35f, 0f, 109.55f), new Vector3(1f, 0f, 0f), 14f, new Vector3(0f, 0f, 1f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(49f, 0f, 109.55f), new Vector3(1f, 0f, 0f), 15f, new Vector3(0f, 0f, 1f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(-4f, 0f, 371.55f), new Vector3(1f, 0f, 0f), 15f, new Vector3(0f, 0f, 1f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(11f, 0f, 371.55f), new Vector3(1f, 0f, 0f), 14f, new Vector3(0f, 0f, 1f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(25f, 0f, 371.55f), new Vector3(1f, 0f, 0f), 15f, new Vector3(0f, 0f, 1f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(64.45f, 0f, 200f), new Vector3(0f, 0f, 1f), 13f, new Vector3(-1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(64.45f, 0f, 213f), new Vector3(0f, 0f, 1f), 12f, new Vector3(-1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(64.45f, 0f, 225f), new Vector3(0f, 0f, 1f), 13f, new Vector3(-1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(23.55f, 0f, 200f), new Vector3(0f, 0f, 1f), 12f, new Vector3(1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(23.55f, 0f, 212f), new Vector3(0f, 0f, 1f), 12f, new Vector3(1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(23.55f, 0f, 224f), new Vector3(0f, 0f, 1f), 12f, new Vector3(1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(-8.45f, 0f, 58f), new Vector3(0f, 0f, 1f), 11f, new Vector3(1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(-8.45f, 0f, 69f), new Vector3(0f, 0f, 1f), 12f, new Vector3(1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(-8.45f, 0f, 81f), new Vector3(0f, 0f, 1f), 11f, new Vector3(1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(8.45f, 0f, 58f), new Vector3(0f, 0f, 1f), 11f, new Vector3(-1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(8.45f, 0f, 69f), new Vector3(0f, 0f, 1f), 12f, new Vector3(-1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(8.45f, 0f, 81f), new Vector3(0f, 0f, 1f), 11f, new Vector3(-1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(24f, 0f, 244.45f), new Vector3(1f, 0f, 0f), 11f, new Vector3(0f, 0f, -1f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(35f, 0f, 244.45f), new Vector3(1f, 0f, 0f), 12f, new Vector3(0f, 0f, -1f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(47f, 0f, 244.45f), new Vector3(1f, 0f, 0f), 11f, new Vector3(0f, 0f, -1f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(-20.45f, 0f, 340f), new Vector3(0f, 0f, 1f), 16f, new Vector3(1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(-20.45f, 0f, 356f), new Vector3(0f, 0f, 1f), 16f, new Vector3(1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(-3.55f, 0f, 340f), new Vector3(0f, 0f, 1f), 16f, new Vector3(-1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(-16.45f, 0f, 10f), new Vector3(0f, 0f, 1f), 12f, new Vector3(1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(-16.45f, 0f, 22f), new Vector3(0f, 0f, 1f), 12f, new Vector3(1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(16.45f, 0f, 10f), new Vector3(0f, 0f, 1f), 12f, new Vector3(-1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(16.45f, 0f, 22f), new Vector3(0f, 0f, 1f), 12f, new Vector3(-1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(20f, 0f, 188.45f), new Vector3(1f, 0f, 0f), 12f, new Vector3(0f, 0f, -1f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(32f, 0f, 188.45f), new Vector3(1f, 0f, 0f), 12f, new Vector3(0f, 0f, -1f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(8.45f, 0f, 104f), new Vector3(0f, 0f, 1f), 9f, new Vector3(-1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(8.45f, 0f, 113f), new Vector3(0f, 0f, 1f), 9f, new Vector3(-1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(-16.45f, 0f, 38f), new Vector3(0f, 0f, 1f), 16f, new Vector3(1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(16.45f, 0f, 38f), new Vector3(0f, 0f, 1f), 16f, new Vector3(-1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(-4.45f, 0f, -4f), new Vector3(0f, 0f, 1f), 14f, new Vector3(1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(4.45f, 0f, -4f), new Vector3(0f, 0f, 1f), 14f, new Vector3(-1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(54.45f, 0f, 188f), new Vector3(0f, 0f, 1f), 10f, new Vector3(-1f, 0f, 0f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(-11.55f, 0f, 42f), new Vector3(0f, 0f, 1f), 8f, new Vector3(-1f, 0f, 0f), 3.55f, LowBuildings, true);
        BuildingRow(g, new Vector3(-3.55f, 0f, 18f), new Vector3(0f, 0f, 1f), 8f, new Vector3(-1f, 0f, 0f), 3.55f, LowBuildings, true);
        BuildingRow(g, new Vector3(-3.55f, 0f, 30f), new Vector3(0f, 0f, 1f), 8f, new Vector3(-1f, 0f, 0f), 5.55f, LowBuildings, true);
        BuildingRow(g, new Vector3(-4.45f, 0f, 42f), new Vector3(0f, 0f, 1f), 8f, new Vector3(1f, 0f, 0f), 3.55f, LowBuildings, true);
        BuildingRow(g, new Vector3(3.55f, 0f, 18f), new Vector3(0f, 0f, 1f), 8f, new Vector3(1f, 0f, 0f), 3.55f, LowBuildings, true);
        BuildingRow(g, new Vector3(11.55f, 0f, 42f), new Vector3(0f, 0f, 1f), 8f, new Vector3(1f, 0f, 0f), 9.55f, Buildings, true);
        BuildingRow(g, new Vector3(-4f, 0f, -4.45f), new Vector3(1f, 0f, 0f), 8f, new Vector3(0f, 0f, 1f), 11.55f, Buildings, true);
        BuildingRow(g, new Vector3(-21f, 0f, 200f), new Vector3(0f, 0f, 1f), 140f, new Vector3(1f, 0f, 0f), 12f, Buildings, true);
        BuildingRow(g, new Vector3(-2.6f, 0f, 200f), new Vector3(0f, 0f, 1f), 34f, new Vector3(-1f, 0f, 0f), 12f, Buildings, true);
        BuildingRow(g, new Vector3(-2.6f, 0f, 247f), new Vector3(0f, 0f, 1f), 90f, new Vector3(-1f, 0f, 0f), 12f, Buildings, true);
        Box("BrickWall", g, new Vector3(-6f, 3.1f, 15f), new Vector3(12f, 6.2f, 2f), "Brick", true);
        Box("BrickWall", g, new Vector3(6f, 2.65f, 15f), new Vector3(12f, 5.3f, 2f), "Brick", true);
        Box("BrickWall", g, new Vector3(9f, 2.3f, 30f), new Vector3(2f, 4.6f, 16f), "Brick", true);
        Box("BrickWall", g, new Vector3(57.5f, 2.65f, 199.5f), new Vector3(13f, 5.3f, 1f), "Brick", true);
        Box("BrickWall", g, new Vector3(1f, 2.55f, 44f), new Vector3(2f, 5.1f, 12f), "Brick", true);
        Box("BrickWall", g, new Vector3(3f, 2.45f, 36f), new Vector3(2f, 4.9f, 12f), "Brick", true);
        Box("BrickWall", g, new Vector3(11f, 2.75f, 28f), new Vector3(2f, 5.5f, 12f), "Brick", true);
        Box("BrickWall", g, new Vector3(-10f, 3.05f, 55f), new Vector3(12f, 6.1f, 2f), "Brick", true);
        Box("BrickWall", g, new Vector3(10f, 2.4f, 55f), new Vector3(12f, 4.8f, 2f), "Brick", true);
        Box("BrickWall", g, new Vector3(-11f, 2.8f, 26f), new Vector3(2f, 5.6f, 8f), "Brick", true);
        Box("BrickWall", g, new Vector3(-9f, 2.9f, 26f), new Vector3(2f, 5.8f, 8f), "Brick", true);
        Box("BrickWall", g, new Vector3(-8f, 2.35f, 17f), new Vector3(8f, 4.7f, 2f), "Brick", true);
        Box("BrickWall", g, new Vector3(8f, 2.4f, 17f), new Vector3(8f, 4.8f, 2f), "Brick", true);
        Box("BrickWall", g, new Vector3(-12f, 2.4f, 35f), new Vector3(8f, 4.8f, 2f), "Brick", true);
        Box("BrickWall", g, new Vector3(-12f, 2.6f, 37f), new Vector3(8f, 5.2f, 2f), "Brick", true);
        Box("BrickWall", g, new Vector3(14f, 2.4f, 35f), new Vector3(4f, 4.8f, 2f), "Brick", true);
        Box("BrickWall", g, new Vector3(-6f, 2.5f, 57f), new Vector3(4f, 5f, 2f), "Brick", true);
        Box("BrickWall", g, new Vector3(6f, 2.95f, 57f), new Vector3(4f, 5.9f, 2f), "Brick", true);
        Box("BrickWall", g, new Vector3(45.5f, 2.9f, 198.5f), new Vector3(3f, 5.8f, 1f), "Brick", true);
        Box("BrickWall", g, new Vector3(52.5f, 2.5f, 198.5f), new Vector3(3f, 5f, 1f), "Brick", true);
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(-78f, 0f, 0f), 90f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(82f, 0f, 0f), -90f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(-78f, 0f, 34f), 90f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(82f, 0f, 34f), -90f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(-78f, 0f, 68f), 90f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(82f, 0f, 68f), -90f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(-78f, 0f, 102f), 90f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(82f, 0f, 102f), -90f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(-78f, 0f, 136f), 90f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(82f, 0f, 136f), -90f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(-78f, 0f, 170f), 90f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(82f, 0f, 170f), -90f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(-78f, 0f, 204f), 90f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(82f, 0f, 204f), -90f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(-78f, 0f, 238f), 90f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(82f, 0f, 238f), -90f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(-78f, 0f, 272f), 90f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(82f, 0f, 272f), -90f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(-78f, 0f, 306f), 90f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(82f, 0f, 306f), -90f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(-78f, 0f, 340f), 90f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(82f, 0f, 340f), -90f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(-78f, 0f, 374f), 90f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(82f, 0f, 374f), -90f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(-78f, 0f, 408f), 90f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(82f, 0f, 408f), -90f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(-70f, 0f, 450f), 180f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(-52f, 0f, 450f), 180f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(-34f, 0f, 450f), 180f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(-16f, 0f, 450f), 180f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(2f, 0f, 450f), 180f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(20f, 0f, 450f), 180f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(38f, 0f, 450f), 180f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(56f, 0f, 450f), 180f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
        Place(Kit.Commercial, Pick(Skyscrapers), g, new Vector3(74f, 0f, 450f), 180f, CityScale * 1.4f, Vector3.one, false, false, Pick("K_Commercial", "K_CommercialA", "K_CommercialB"));
    }

    private static void BuildGeneratedProps(Transform g)
    {
        WallLamp(g, new Vector3(-17.6f, 2.8f, 12f), new Vector3(1f, 0f, 0f), MapFlickerLight.FlickerMode.Fire);
        WallLamp(g, new Vector3(17.6f, 2.8f, 20f), new Vector3(-1f, 0f, 0f), MapFlickerLight.FlickerMode.Broken);
        WallLamp(g, new Vector3(-13.6f, 2.8f, 30f), new Vector3(1f, 0f, 0f), MapFlickerLight.FlickerMode.Fire);
        WallLamp(g, new Vector3(6.4f, 2.8f, 44f), new Vector3(-1f, 0f, 0f), MapFlickerLight.FlickerMode.Broken);
        WallLamp(g, new Vector3(-1.6f, 2.8f, 52f), new Vector3(1f, 0f, 0f), MapFlickerLight.FlickerMode.Broken);
        WallLamp(g, new Vector3(17.6f, 2.8f, 48f), new Vector3(-1f, 0f, 0f), MapFlickerLight.FlickerMode.Broken);
        WallLamp(g, new Vector3(-9.6f, 2.8f, 40f), new Vector3(-1f, 0f, 0f), MapFlickerLight.FlickerMode.Fire);
        Place(Kit.Roads, "dumpster", g, new Vector3(-16.5f, 0f, 20f), 0f, CityScale, Vector3.one, true, false, "K_Roads");
        Place(Kit.Roads, "dumpster", g, new Vector3(16.5f, 0f, 44f), 90f, CityScale, Vector3.one, true, false, "K_Roads");
        Place(Kit.Cars, "box", g, new Vector3(-12.5f, 0f, 9f), 20f, CarScale, Vector3.one, true, false, "K_Cars");
        Place(Kit.Cars, "box", g, new Vector3(10.5f, 0f, 51f), -30f, CarScale, Vector3.one, true, false, "K_Cars");
        Breakable(g, false, new Vector3(13.5f, 0.5f, 31f));
        Breakable(g, true, new Vector3(-14.7f, 1f, 47f));
        Breakable(g, false, new Vector3(-3.2f, 0.5f, 25f));
        Place(Kit.Cars, "cone", g, new Vector3(-1f, 0f, 33f), 209.8f, CarScale, Vector3.one, false, false, "K_Cars");
        Place(Kit.Cars, "cone", g, new Vector3(1.5f, 0f, 21f), 327.49f, CarScale, Vector3.one, false, false, "K_Cars");
        Place(Kit.Cars, "cone", g, new Vector3(-17f, 0f, 37f), 77.29f, CarScale, Vector3.one, false, false, "K_Cars");
        Car(g, "sedan", new Vector3(-2.5f, 0f, 66f), 15f, false);
        Car(g, "taxi", new Vector3(3f, 0f, 74f), 170f, false);
        Car(g, "suv", new Vector3(-1.5f, 0f, 84f), 75f, true);
        Car(g, "van", new Vector3(3.2f, 0f, 112f), -10f, false);
        Car(g, "hatchback-sports", new Vector3(-3f, 0f, 118f), 200f, false);
        Car(g, "delivery", new Vector3(2.5f, 0f, 128f), 5f, false);
        Car(g, "sedan-sports", new Vector3(-2f, 0f, 140f), 160f, false);
        Car(g, "police", new Vector3(-13f, 0f, 98f), 80f, false);
        Car(g, "ambulance", new Vector3(13f, 0f, 99f), -100f, false);
        { Light l = AddLight(g, "Light", new Vector3(-13f, 2f, 98f), LightType.Point, new Color(0.2f, 0.4f, 1f), 2f, 10f, 0f, Vector3.zero); AddFlicker(l, MapFlickerLight.FlickerMode.Siren); }
        { Light l = AddLight(g, "Light", new Vector3(13f, 2.6f, 99f), LightType.Point, new Color(1f, 0.15f, 0.1f), 2f, 10f, 0f, Vector3.zero); AddFlicker(l, MapFlickerLight.FlickerMode.Siren); }
        Barricade(g, new Vector3(-18f, 0f, 98f), 90f, 12f);
        Barricade(g, new Vector3(18f, 0f, 98f), 90f, 12f);
        Car(g, "truck", new Vector3(-3.5f, 0f, 151f), 90f, false);
        Car(g, "garbage-truck", new Vector3(3.5f, 0f, 150f), -80f, true);
        Barricade(g, new Vector3(0f, 0f, 149f), 0f, 16f);
        StreetLight(g, new Vector3(-6.8f, 0.15f, 64f), new Vector3(1f, 0f, 0f), true);
        StreetLight(g, new Vector3(6.8f, 0.15f, 74f), new Vector3(-1f, 0f, 0f), false);
        StreetLight(g, new Vector3(-6.8f, 0.15f, 84f), new Vector3(1f, 0f, 0f), true);
        StreetLight(g, new Vector3(6.8f, 0.15f, 94f), new Vector3(-1f, 0f, 0f), false);
        StreetLight(g, new Vector3(-6.8f, 0.15f, 124f), new Vector3(1f, 0f, 0f), true);
        StreetLight(g, new Vector3(-6.8f, 0.15f, 144f), new Vector3(1f, 0f, 0f), false);
        StreetLight(g, new Vector3(6.8f, 0.15f, 154f), new Vector3(-1f, 0f, 0f), false);
        Place(Kit.Cars, "cone", g, new Vector3(-1f, 0f, 61f), 227.03f, CarScale, Vector3.one, false, false, "K_Cars");
        Place(Kit.Cars, "cone", g, new Vector3(0.5f, 0f, 62f), 209.88f, CarScale, Vector3.one, false, false, "K_Cars");
        Place(Kit.Cars, "cone", g, new Vector3(1.2f, 0f, 87f), 22.27f, CarScale, Vector3.one, false, false, "K_Cars");
        Place(Kit.Cars, "cone", g, new Vector3(-4f, 0f, 133f), 210.79f, CarScale, Vector3.one, false, false, "K_Cars");
        Place(Kit.Cars, "debris-tire", g, new Vector3(-3.6f, 0f, 80.57f), 200.4f, CarScale, Vector3.one, false, false, "K_Cars");
        Place(Kit.Cars, "debris-door", g, new Vector3(-2.93f, 0f, 97.21f), 194.65f, CarScale, Vector3.one, false, false, "K_Cars");
        Place(Kit.Cars, "debris-bumper", g, new Vector3(0.57f, 0f, 109.06f), 245.52f, CarScale, Vector3.one, false, false, "K_Cars");
        Place(Kit.Cars, "debris-plate-a", g, new Vector3(-3.18f, 0f, 109.98f), 67.63f, CarScale, Vector3.one, false, false, "K_Cars");
        Breakable(g, true, new Vector3(-6f, 1.15f, 70f));
        Breakable(g, true, new Vector3(-5.6f, 1.15f, 71.2f));
        Breakable(g, false, new Vector3(6f, 0.65f, 80f));
        Car(g, "delivery-flat", new Vector3(14f, 0f, 126f), 30f, false);
        StreetLight(g, new Vector3(12f, 0f, 136.5f), new Vector3(0f, 0f, -1f), false);
        Fountain(g, new Vector3(42f, 0f, 150f));
        Box("Hedge", g, new Vector3(30f, 0.55f, 114.5f), new Vector3(12f, 1.1f, 1f), "Leaves", true);
        Box("Hedge", g, new Vector3(54f, 0.55f, 120.5f), new Vector3(12f, 1.1f, 1f), "Leaves", true);
        Box("Hedge", g, new Vector3(30f, 0.55f, 140.5f), new Vector3(12f, 1.1f, 1f), "Leaves", true);
        Box("Hedge", g, new Vector3(54f, 0.55f, 146.5f), new Vector3(12f, 1.1f, 1f), "Leaves", true);
        Box("Hedge", g, new Vector3(30f, 0.55f, 162.5f), new Vector3(12f, 1.1f, 1f), "Leaves", true);
        Box("Hedge", g, new Vector3(54f, 0.55f, 168.5f), new Vector3(12f, 1.1f, 1f), "Leaves", true);
        Tree(g, new Vector3(25.9f, 0f, 164.7f), 1.15f);
        Tree(g, new Vector3(29.19f, 0f, 169.71f), 0.98f);
        Tree(g, new Vector3(34.01f, 0f, 148.64f), 1.07f);
        Tree(g, new Vector3(24.93f, 0f, 149.88f), 1.01f);
        Tree(g, new Vector3(35.68f, 0f, 181.06f), 1.1f);
        Tree(g, new Vector3(60.48f, 0f, 117.74f), 1.15f);
        Tree(g, new Vector3(53.56f, 0f, 172.56f), 1.07f);
        Tree(g, new Vector3(53.88f, 0f, 117.09f), 0.98f);
        Tree(g, new Vector3(32.8f, 0f, 163.58f), 0.97f);
        Tree(g, new Vector3(45.12f, 0f, 162.41f), 1.11f);
        Tree(g, new Vector3(50.67f, 0f, 177.64f), 1.07f);
        Tree(g, new Vector3(59.63f, 0f, 138.3f), 1.16f);
        Tree(g, new Vector3(27.48f, 0f, 143.86f), 1.14f);
        Tree(g, new Vector3(49.31f, 0f, 140.15f), 1.03f);
        Tree(g, new Vector3(25.32f, 0f, 123.2f), 1.18f);
        Tree(g, new Vector3(22.48f, 0f, 173.5f), 1.01f);
        Tree(g, new Vector3(33.28f, 0f, 122.78f), 1.14f);
        Tree(g, new Vector3(27.02f, 0f, 175.58f), 1.28f);
        Tree(g, new Vector3(48.2f, 0f, 166.74f), 1.11f);
        Tree(g, new Vector3(56.84f, 0f, 182.44f), 1.19f);
        Tree(g, new Vector3(38.02f, 0f, 126.11f), 1.29f);
        Tree(g, new Vector3(46.03f, 0f, 119.58f), 1.15f);
        Tree(g, new Vector3(30.32f, 0f, 139.84f), 1.17f);
        Tree(g, new Vector3(60.22f, 0f, 156.57f), 1.12f);
        Tree(g, new Vector3(61.11f, 0f, 147.55f), 1.06f);
        Tree(g, new Vector3(51.61f, 0f, 147.42f), 1.19f);
        Tree(g, new Vector3(55.82f, 0f, 150.36f), 1.27f);
        Tree(g, new Vector3(47.46f, 0f, 157.38f), 1.23f);
        Tree(g, new Vector3(52.33f, 0f, 126.44f), 1.03f);
        Tree(g, new Vector3(38.03f, 0f, 171.45f), 1.02f);
        Tree(g, new Vector3(61.58f, 0f, 170.47f), 1.12f);
        Tree(g, new Vector3(29.75f, 0f, 156.78f), 1.07f);
        Tree(g, new Vector3(54.34f, 0f, 165.51f), 1.07f);
        Tree(g, new Vector3(22.08f, 0f, 179.28f), 1.07f);
        Tree(g, new Vector3(47.73f, 0f, 173.76f), 0.99f);
        Tree(g, new Vector3(37.54f, 0f, 164.65f), 1.02f);
        Tree(g, new Vector3(57.56f, 0f, 144.11f), 1.17f);
        Tree(g, new Vector3(28.35f, 0f, 185.49f), 0.96f);
        Tree(g, new Vector3(59.5f, 0f, 123.54f), 1.14f);
        Tree(g, new Vector3(57.91f, 0f, 161.02f), 1.24f);
        Tree(g, new Vector3(56.91f, 0f, 169.46f), 1.16f);
        Tree(g, new Vector3(26.81f, 0f, 116.57f), 1.19f);
        Tree(g, new Vector3(46.25f, 0f, 126.76f), 1.05f);
        Tree(g, new Vector3(36.26f, 0f, 118.82f), 1.08f);
        Tree(g, new Vector3(58.74f, 0f, 128.91f), 1.26f);
        Tree(g, new Vector3(32.76f, 0f, 113.25f), 0.98f);
        Bench(g, new Vector3(38f, 0f, 120f), 90f);
        Bench(g, new Vector3(46f, 0f, 124f), -90f);
        Bench(g, new Vector3(38f, 0f, 170f), 90f);
        Bench(g, new Vector3(46f, 0f, 176f), -90f);
        Bench(g, new Vector3(30f, 0f, 131f), 0f);
        Bench(g, new Vector3(56f, 0f, 137f), 180f);
        StreetLight(g, new Vector3(38f, 0f, 118f), new Vector3(1f, 0f, 0f), true);
        StreetLight(g, new Vector3(46f, 0f, 146f), new Vector3(-1f, 0f, 0f), true);
        StreetLight(g, new Vector3(38f, 0f, 166f), new Vector3(1f, 0f, 0f), false);
        StreetLight(g, new Vector3(46f, 0f, 184f), new Vector3(-1f, 0f, 0f), false);
        StreetLight(g, new Vector3(26f, 0f, 134f), new Vector3(1f, 0f, 0f), true);
        StreetLight(g, new Vector3(60f, 0f, 134f), new Vector3(-1f, 0f, 0f), true);
        Car(g, "ambulance", new Vector3(56f, 0f, 118f), 40f, true);
        Breakable(g, true, new Vector3(30f, 1f, 172f));
        Breakable(g, true, new Vector3(31.2f, 1f, 173f));
        Breakable(g, false, new Vector3(58f, 0.5f, 160f));
        Box("Pillar", g, new Vector3(28f, 2.75f, 206f), new Vector3(0.9f, 5.5f, 0.9f), "Concrete", true);
        Box("Pillar", g, new Vector3(28f, 2.75f, 214f), new Vector3(0.9f, 5.5f, 0.9f), "Concrete", true);
        Box("Pillar", g, new Vector3(28f, 2.75f, 222f), new Vector3(0.9f, 5.5f, 0.9f), "Concrete", true);
        Box("Pillar", g, new Vector3(28f, 2.75f, 230f), new Vector3(0.9f, 5.5f, 0.9f), "Concrete", true);
        Box("Pillar", g, new Vector3(28f, 2.75f, 238f), new Vector3(0.9f, 5.5f, 0.9f), "Concrete", true);
        Box("Pillar", g, new Vector3(36f, 2.75f, 206f), new Vector3(0.9f, 5.5f, 0.9f), "Concrete", true);
        Box("Pillar", g, new Vector3(36f, 2.75f, 214f), new Vector3(0.9f, 5.5f, 0.9f), "Concrete", true);
        Box("Pillar", g, new Vector3(36f, 2.75f, 222f), new Vector3(0.9f, 5.5f, 0.9f), "Concrete", true);
        Box("Pillar", g, new Vector3(36f, 2.75f, 230f), new Vector3(0.9f, 5.5f, 0.9f), "Concrete", true);
        Box("Pillar", g, new Vector3(36f, 2.75f, 238f), new Vector3(0.9f, 5.5f, 0.9f), "Concrete", true);
        Box("Pillar", g, new Vector3(44f, 2.75f, 206f), new Vector3(0.9f, 5.5f, 0.9f), "Concrete", true);
        Box("Pillar", g, new Vector3(44f, 2.75f, 214f), new Vector3(0.9f, 5.5f, 0.9f), "Concrete", true);
        Box("Pillar", g, new Vector3(44f, 2.75f, 222f), new Vector3(0.9f, 5.5f, 0.9f), "Concrete", true);
        Box("Pillar", g, new Vector3(44f, 2.75f, 230f), new Vector3(0.9f, 5.5f, 0.9f), "Concrete", true);
        Box("Pillar", g, new Vector3(44f, 2.75f, 238f), new Vector3(0.9f, 5.5f, 0.9f), "Concrete", true);
        Box("Pillar", g, new Vector3(52f, 2.75f, 206f), new Vector3(0.9f, 5.5f, 0.9f), "Concrete", true);
        Box("Pillar", g, new Vector3(52f, 2.75f, 214f), new Vector3(0.9f, 5.5f, 0.9f), "Concrete", true);
        Box("Pillar", g, new Vector3(52f, 2.75f, 222f), new Vector3(0.9f, 5.5f, 0.9f), "Concrete", true);
        Box("Pillar", g, new Vector3(52f, 2.75f, 230f), new Vector3(0.9f, 5.5f, 0.9f), "Concrete", true);
        Box("Pillar", g, new Vector3(52f, 2.75f, 238f), new Vector3(0.9f, 5.5f, 0.9f), "Concrete", true);
        Car(g, "sedan", new Vector3(26.5f, 0f, 210f), 90f, false);
        Car(g, "hatchback-sports", new Vector3(55.5f, 0f, 210f), -90f, false);
        Car(g, "suv", new Vector3(26.5f, 0f, 218f), 90f, false);
        Car(g, "van", new Vector3(26.5f, 0f, 226f), 90f, false);
        Car(g, "taxi", new Vector3(55.5f, 0f, 226f), -90f, false);
        Car(g, "sedan-sports", new Vector3(26.5f, 0f, 234f), 90f, true);
        Car(g, "police", new Vector3(26.5f, 0f, 242f), 90f, false);
        Car(g, "suv-luxury", new Vector3(55.5f, 0f, 242f), -90f, false);
        { Light l = AddLight(g, "Light", new Vector3(30f, 5.1f, 210f), LightType.Point, new Color(1f, 0.85f, 0.6f), 1.6f, 9f, 0f, Vector3.zero); AddFlicker(l, MapFlickerLight.FlickerMode.Broken); }
        { Light l = AddLight(g, "Light", new Vector3(30f, 5.1f, 224f), LightType.Point, new Color(1f, 0.85f, 0.6f), 1.6f, 9f, 0f, Vector3.zero); }
        { Light l = AddLight(g, "Light", new Vector3(30f, 5.1f, 238f), LightType.Point, new Color(1f, 0.85f, 0.6f), 1.6f, 9f, 0f, Vector3.zero); }
        { Light l = AddLight(g, "Light", new Vector3(42f, 5.1f, 210f), LightType.Point, new Color(1f, 0.85f, 0.6f), 1.6f, 9f, 0f, Vector3.zero); }
        { Light l = AddLight(g, "Light", new Vector3(42f, 5.1f, 224f), LightType.Point, new Color(1f, 0.85f, 0.6f), 1.6f, 9f, 0f, Vector3.zero); AddFlicker(l, MapFlickerLight.FlickerMode.Broken); }
        { Light l = AddLight(g, "Light", new Vector3(42f, 5.1f, 238f), LightType.Point, new Color(1f, 0.85f, 0.6f), 1.6f, 9f, 0f, Vector3.zero); }
        { Light l = AddLight(g, "Light", new Vector3(54f, 5.1f, 210f), LightType.Point, new Color(1f, 0.85f, 0.6f), 1.6f, 9f, 0f, Vector3.zero); }
        { Light l = AddLight(g, "Light", new Vector3(54f, 5.1f, 224f), LightType.Point, new Color(1f, 0.85f, 0.6f), 1.6f, 9f, 0f, Vector3.zero); }
        { Light l = AddLight(g, "Light", new Vector3(54f, 5.1f, 238f), LightType.Point, new Color(1f, 0.85f, 0.6f), 1.6f, 9f, 0f, Vector3.zero); }
        Breakable(g, false, new Vector3(36f, 0.5f, 214f));
        Breakable(g, true, new Vector3(48f, 1f, 236f));
        Car(g, "sedan", new Vector3(30f, 6f, 210f), 90f, false);
        Car(g, "van", new Vector3(44f, 6f, 222f), 0f, false);
        Car(g, "suv", new Vector3(52f, 6f, 232f), 180f, false);
        Car(g, "police", new Vector3(34f, 6f, 238f), 70f, false);
        StreetLight(g, new Vector3(26f, 6f, 206f), new Vector3(1f, 0f, 0f), false);
        StreetLight(g, new Vector3(56f, 6f, 206f), new Vector3(-1f, 0f, 0f), false);
        StreetLight(g, new Vector3(26f, 6f, 242f), new Vector3(1f, 0f, 0f), false);
        Box("Pillar", g, new Vector3(4f, 2.75f, 240f), new Vector3(1.4f, 5.5f, 1.4f), "Concrete", true);
        Box("Pillar", g, new Vector3(14f, 2.75f, 240f), new Vector3(1.4f, 5.5f, 1.4f), "Concrete", true);
        Box("Pillar", g, new Vector3(-18f, 2.75f, 204f), new Vector3(1.2f, 5.5f, 1.2f), "Concrete", true);
        Box("Pillar", g, new Vector3(-6f, 2.75f, 204f), new Vector3(1.2f, 5.5f, 1.2f), "Concrete", true);
        Box("Pillar", g, new Vector3(-18f, 2.75f, 216f), new Vector3(1.2f, 5.5f, 1.2f), "Concrete", true);
        Box("Pillar", g, new Vector3(-6f, 2.75f, 216f), new Vector3(1.2f, 5.5f, 1.2f), "Concrete", true);
        Box("Pillar", g, new Vector3(-18f, 2.75f, 228f), new Vector3(1.2f, 5.5f, 1.2f), "Concrete", true);
        Box("Pillar", g, new Vector3(-6f, 2.75f, 228f), new Vector3(1.2f, 5.5f, 1.2f), "Concrete", true);
        Box("Pillar", g, new Vector3(-18f, 2.75f, 240f), new Vector3(1.2f, 5.5f, 1.2f), "Concrete", true);
        Box("Pillar", g, new Vector3(-6f, 2.75f, 240f), new Vector3(1.2f, 5.5f, 1.2f), "Concrete", true);
        Box("Pillar", g, new Vector3(-18f, 2.75f, 252f), new Vector3(1.2f, 5.5f, 1.2f), "Concrete", true);
        Box("Pillar", g, new Vector3(-6f, 2.75f, 252f), new Vector3(1.2f, 5.5f, 1.2f), "Concrete", true);
        Box("Pillar", g, new Vector3(-18f, 2.75f, 264f), new Vector3(1.2f, 5.5f, 1.2f), "Concrete", true);
        Box("Pillar", g, new Vector3(-6f, 2.75f, 264f), new Vector3(1.2f, 5.5f, 1.2f), "Concrete", true);
        Box("Pillar", g, new Vector3(-18f, 2.75f, 276f), new Vector3(1.2f, 5.5f, 1.2f), "Concrete", true);
        Box("Pillar", g, new Vector3(-6f, 2.75f, 276f), new Vector3(1.2f, 5.5f, 1.2f), "Concrete", true);
        Box("Pillar", g, new Vector3(-18f, 2.75f, 288f), new Vector3(1.2f, 5.5f, 1.2f), "Concrete", true);
        Box("Pillar", g, new Vector3(-6f, 2.75f, 288f), new Vector3(1.2f, 5.5f, 1.2f), "Concrete", true);
        Box("Pillar", g, new Vector3(-18f, 2.75f, 300f), new Vector3(1.2f, 5.5f, 1.2f), "Concrete", true);
        Box("Pillar", g, new Vector3(-6f, 2.75f, 300f), new Vector3(1.2f, 5.5f, 1.2f), "Concrete", true);
        Box("Pillar", g, new Vector3(-18f, 2.75f, 312f), new Vector3(1.2f, 5.5f, 1.2f), "Concrete", true);
        Box("Pillar", g, new Vector3(-6f, 2.75f, 312f), new Vector3(1.2f, 5.5f, 1.2f), "Concrete", true);
        Box("Pillar", g, new Vector3(-18f, 2.75f, 324f), new Vector3(1.2f, 5.5f, 1.2f), "Concrete", true);
        Box("Pillar", g, new Vector3(-6f, 2.75f, 324f), new Vector3(1.2f, 5.5f, 1.2f), "Concrete", true);
        Box("Pillar", g, new Vector3(-18f, 2.75f, 336f), new Vector3(1.2f, 5.5f, 1.2f), "Concrete", true);
        Box("Pillar", g, new Vector3(-6f, 2.75f, 336f), new Vector3(1.2f, 5.5f, 1.2f), "Concrete", true);
        Car(g, "sedan", new Vector3(-15.76f, 6f, 212f), 3.63f, false);
        Car(g, "taxi", new Vector3(-9.44f, 6f, 221f), -0.13f, false);
        Car(g, "suv", new Vector3(-15.68f, 6f, 230f), 8.88f, false);
        Car(g, "van", new Vector3(-9.19f, 6f, 239f), 1.02f, true);
        Car(g, "truck", new Vector3(-15.7f, 6f, 248f), -6.86f, false);
        Car(g, "delivery", new Vector3(-15.66f, 6f, 266f), -13.46f, false);
        Car(g, "sedan-sports", new Vector3(-7.73f, 6f, 275f), -2.58f, false);
        Car(g, "suv-luxury", new Vector3(-14.97f, 6f, 284f), 9.07f, false);
        Car(g, "garbage-truck", new Vector3(-8.72f, 6f, 293f), -2.85f, true);
        Car(g, "sedan", new Vector3(-9.19f, 6f, 311f), -11.59f, false);
        Car(g, "taxi", new Vector3(-15.34f, 6f, 320f), -4.06f, false);
        Car(g, "suv", new Vector3(-9.24f, 6f, 329f), -13.45f, false);
        StreetLight(g, new Vector3(-19.6f, 6f, 206f), new Vector3(1f, 0f, 0f), true);
        StreetLight(g, new Vector3(-19.6f, 6f, 230f), new Vector3(1f, 0f, 0f), false);
        StreetLight(g, new Vector3(-19.6f, 6f, 254f), new Vector3(1f, 0f, 0f), false);
        StreetLight(g, new Vector3(-19.6f, 6f, 278f), new Vector3(1f, 0f, 0f), false);
        StreetLight(g, new Vector3(-19.6f, 6f, 302f), new Vector3(1f, 0f, 0f), false);
        StreetLight(g, new Vector3(-19.6f, 6f, 326f), new Vector3(1f, 0f, 0f), false);
        Gantry(g, new Vector3(-12f, DeckY, 262f));
        Gantry(g, new Vector3(-12f, DeckY, 318f));
        Barricade(g, new Vector3(-12f, 6f, 204f), 0f, 16f);
        Breakable(g, true, new Vector3(-6f, 7f, 300f));
        Breakable(g, false, new Vector3(-17f, 6.5f, 284f));
        Helipad(g, new Vector3(0f, 0f, 418f));
        Sandbags(g, new Vector3(-8f, 0f, 406f), 0f, 7f);
        Sandbags(g, new Vector3(8f, 0f, 406f), 0f, 7f);
        Sandbags(g, new Vector3(-12f, 0f, 414f), 90f, 6f);
        Sandbags(g, new Vector3(12f, 0f, 414f), 90f, 6f);
        Sandbags(g, new Vector3(-8f, 0f, 426f), 0f, 7f);
        Sandbags(g, new Vector3(8f, 0f, 426f), 0f, 7f);
        Box("Block", g, new Vector3(-24f, 0.6f, 388f), new Vector3(3f, 1.2f, 1f), "Concrete", true);
        Box("Block", g, new Vector3(23f, 0.6f, 392f), new Vector3(1f, 1.2f, 3f), "Concrete", true);
        Box("Block", g, new Vector3(-27f, 0.6f, 418f), new Vector3(1f, 1.2f, 3f), "Concrete", true);
        Box("Block", g, new Vector3(28f, 0.6f, 420f), new Vector3(3f, 1.2f, 1f), "Concrete", true);
        Box("Block", g, new Vector3(-10f, 0.6f, 380f), new Vector3(3f, 1.2f, 1f), "Concrete", true);
        Box("Block", g, new Vector3(12f, 0.6f, 384f), new Vector3(1f, 1.2f, 3f), "Concrete", true);
        Car(g, "police", new Vector3(-18f, 0f, 406f), 30f, false);
        Car(g, "truck", new Vector3(20f, 0f, 410f), -60f, true);
        Car(g, "ambulance", new Vector3(-30f, 0f, 380f), 120f, false);
        Radio(g, new Vector3(0f, 0f, 404f));
        StreetLight(g, new Vector3(-32f, 0f, 380f), new Vector3(1f, 0f, 0f), true);
        StreetLight(g, new Vector3(32f, 0f, 380f), new Vector3(-1f, 0f, 0f), false);
        StreetLight(g, new Vector3(-32f, 0f, 426f), new Vector3(1f, 0f, 0f), true);
        StreetLight(g, new Vector3(32f, 0f, 426f), new Vector3(-1f, 0f, 0f), true);
        StreetLight(g, new Vector3(0f, 0f, 375f), new Vector3(1f, 0f, 0f), true);
        Breakable(g, true, new Vector3(-16f, 1f, 430f));
        Breakable(g, true, new Vector3(16f, 1f, 376f));
        Breakable(g, false, new Vector3(36f, 0.5f, 400f));
        Breakable(g, false, new Vector3(-36f, 0.5f, 410f));
    }

    private static readonly Vector3[] GeneratedSpawnPoints =
    {
        new Vector3(-14f, 0f, 10f),
        new Vector3(14f, 0f, 10f),
        new Vector3(14f, 0f, 32f),
        new Vector3(-14f, 0f, 46f),
        new Vector3(14f, 0f, 46f),
        new Vector3(-14f, 0f, 30f),
        new Vector3(-15f, 0f, 98f),
        new Vector3(15f, 0f, 98f),
        new Vector3(-5f, 0f, 146f),
        new Vector3(5f, 0f, 146f),
        new Vector3(0f, 0f, 108f),
        new Vector3(24f, 0f, 114f),
        new Vector3(60f, 0f, 114f),
        new Vector3(60f, 0f, 150f),
        new Vector3(24f, 0f, 180f),
        new Vector3(60f, 0f, 184f),
        new Vector3(24f, 0f, 150f),
        new Vector3(28f, 0f, 204f),
        new Vector3(54f, 0f, 204f),
        new Vector3(28f, 0f, 240f),
        new Vector3(40f, 0f, 222f),
        new Vector3(28f, 6f, 204f),
        new Vector3(54f, 6f, 204f),
        new Vector3(30f, 6f, 240f),
        new Vector3(-12f, 6f, 206f),
        new Vector3(-16f, 6f, 260f),
        new Vector3(-8f, 6f, 290f),
        new Vector3(-12f, 6f, 330f),
        new Vector3(-36f, 0f, 376f),
        new Vector3(36f, 0f, 376f),
        new Vector3(-36f, 0f, 428f),
        new Vector3(36f, 0f, 428f),
        new Vector3(0f, 0f, 430f),
        new Vector3(-38f, 0f, 402f),
        new Vector3(38f, 0f, 402f),
    };

    private static readonly Vector3[] GeneratedRoute =
    {
        new Vector3(0f, 0f, 0f),
        new Vector3(-2f, 0f, 8f),
        new Vector3(-2f, 0f, 12f),
        new Vector3(-14f, 0f, 12f),
        new Vector3(-14f, 0f, 32f),
        new Vector3(-6f, 0f, 32f),
        new Vector3(-6f, 0f, 40f),
        new Vector3(-2f, 0f, 40f),
        new Vector3(-2f, 0f, 56f),
        new Vector3(0f, 0f, 64f),
        new Vector3(0f, 0f, 82f),
        new Vector3(0f, 0f, 100f),
        new Vector3(0f, 0f, 116f),
        new Vector3(4f, 0f, 130f),
        new Vector3(14f, 0f, 130f),
        new Vector3(28f, 0f, 134f),
        new Vector3(42f, 0f, 140f),
        new Vector3(42f, 0f, 160f),
        new Vector3(46f, 0f, 176f),
        new Vector3(49f, 0f, 186f),
        new Vector3(49f, 0f, 193f),
        new Vector3(49f, 0f, 200f),
        new Vector3(55f, 0f, 205f),
        new Vector3(61f, 0f, 208f),
        new Vector3(61f, 3f, 222f),
        new Vector3(61f, 6f, 236f),
        new Vector3(61f, 6f, 241f),
        new Vector3(48f, 6f, 240f),
        new Vector3(30f, 6f, 240f),
        new Vector3(10f, 6f, 240f),
        new Vector3(-12f, 6f, 240f),
        new Vector3(-12f, 6f, 270f),
        new Vector3(-12f, 6f, 300f),
        new Vector3(-12f, 6f, 330f),
        new Vector3(-12f, 3f, 356f),
        new Vector3(-8f, 0f, 378f),
        new Vector3(0f, 0f, 392f),
        new Vector3(0f, 0f, 404f),
    };
}
