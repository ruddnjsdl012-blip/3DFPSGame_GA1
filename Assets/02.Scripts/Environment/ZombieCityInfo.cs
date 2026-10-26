using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Tools > Zombie City 메뉴가 만든 도시의 기록.
// 'Remove City' 메뉴로 지울 때 벽과 조명 설정을 원래대로 되돌리는 데 쓴다. 게임 중에는 아무 일도 하지 않는다.
[DisallowMultipleComponent]
public class ZombieCityInfo : MonoBehaviour
{
    [Header("출입구를 내느라 바꾼 원래 맵의 벽")]
    public List<GameObject> DisabledWalls = new List<GameObject>();
    public List<GameObject> WallPieces = new List<GameObject>();

    [Header("적용 전 분위기 설정")]
    public bool AtmosphereSaved;
    public bool Fog;
    public FogMode FogMode;
    public float FogDensity;
    public Color FogColor;
    public AmbientMode AmbientMode;
    public Color AmbientLight;
    public Material Skybox;
    public List<Light> SunLights = new List<Light>();
    public List<float> SunIntensities = new List<float>();
    public List<Color> SunColors = new List<Color>();
    public Camera ViewCamera;
    public CameraClearFlags ClearFlags;
    public Color BackgroundColor;
}
