using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

// 손과 무기만 깊이를 지우고 한 번 더 그린다. URP가 없는 프로젝트도 컴파일된다.
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class FirstPersonWeaponCamera : MonoBehaviour
{
    private Camera _world;
    private Camera _weapon;
    private Light _fill;
    private int _layer = -1;
    private IList _stack;
    private float _scanAt;
    private readonly Dictionary<Camera, int> _removedMasks = new Dictionary<Camera, int>();
    private readonly Dictionary<GameObject, int> _originalLayers = new Dictionary<GameObject, int>();

    public static void Register(Transform root)
    {
        if (root == null) return;
        Camera camera = Camera.main;
        if (camera == null) return;
        FirstPersonWeaponCamera view = camera.GetComponent<FirstPersonWeaponCamera>();
        if (view == null) view = camera.gameObject.AddComponent<FirstPersonWeaponCamera>();
        if (view._layer < 0) return; // 지원하지 않는 파이프라인이면 기존 카메라로 표시한다.
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            GameObject item = child.gameObject;
            if (!view._originalLayers.ContainsKey(item)) view._originalLayers.Add(item, item.layer);
            item.layer = view._layer;
        }
    }

    private static int FindUnusedLayer()
    {
        int used = 0;
        foreach (Transform item in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            used |= 1 << item.gameObject.layer;
        for (int layer = 31; layer >= 8; layer--)
            if ((used & (1 << layer)) == 0 && string.IsNullOrEmpty(LayerMask.LayerToName(layer))) return layer;
        return -1;
    }

    private void Awake()
    {
        _world = GetComponent<Camera>();
        if (_world == null) return;
        int layer = FindUnusedLayer();
        if (layer < 0) return;
        GameObject holder = new GameObject("FirstPersonWeaponCamera");
        holder.transform.SetParent(_world.transform, false);
        _weapon = holder.AddComponent<Camera>();
        _weapon.enabled = false;
        _weapon.CopyFrom(_world);
        _weapon.enabled = false;
        _weapon.clearFlags = CameraClearFlags.Depth;
        _weapon.cullingMask = 1 << layer;
        _weapon.nearClipPlane = 0.01f;
        _weapon.farClipPlane = 5f;
        _weapon.depth = _world.depth + 1f;
        _weapon.depthTextureMode = DepthTextureMode.None;
        _weapon.useOcclusionCulling = false;
        _weapon.allowMSAA = _world.allowMSAA;
        _weapon.eventMask = 0;
        if (!ConfigurePipeline())
        {
            Destroy(holder);
            _weapon = null;
            return;
        }
        _layer = layer;
        ExcludeOtherCameras();
        GameObject lightObject = new GameObject("WeaponFillLight");
        lightObject.layer = layer;
        lightObject.transform.SetParent(holder.transform, false);
        _fill = lightObject.AddComponent<Light>();
        _fill.type = LightType.Point;
        _fill.range = 2f;
        _fill.intensity = 0.7f;
        _fill.shadows = LightShadows.None;
        _fill.cullingMask = 1 << layer;
        _weapon.enabled = true;
        RenderPipelineManager.beginCameraRendering += BeforeRender;
        Camera.onPreCull += BeforeBuiltinRender;
    }

    private bool ConfigurePipeline()
    {
        if (GraphicsSettings.currentRenderPipeline == null) return true;
        try
        {
            Type dataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (dataType == null) return false;
            Component mainData = _world.GetComponent(dataType);
            if (mainData == null) mainData = _world.gameObject.AddComponent(dataType);
            PropertyInfo kind = dataType.GetProperty("renderType");
            if (kind == null || kind.GetValue(mainData).ToString() != "Base") return false;
            Component data = _weapon.gameObject.AddComponent(dataType);
            kind.SetValue(data, Enum.Parse(kind.PropertyType, "Overlay"));
            FieldInfo renderer = dataType.GetField("m_RendererIndex", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo setRenderer = dataType.GetMethod("SetRenderer", new[] { typeof(int) });
            if (renderer != null && setRenderer != null) setRenderer.Invoke(data, new object[] { renderer.GetValue(mainData) });
            Set(dataType, data, "clearDepth", true);
            FieldInfo clearDepth = dataType.GetField("m_ClearDepth", BindingFlags.Instance | BindingFlags.NonPublic);
            if (clearDepth != null) clearDepth.SetValue(data, true);
            Set(dataType, data, "renderShadows", false);
            Set(dataType, data, "renderPostProcessing", false);
            Set(dataType, data, "requiresColorTexture", false);
            Set(dataType, data, "requiresDepthTexture", false);
            Set(dataType, data, "allowXRRendering", false);
            PropertyInfo depth = dataType.GetProperty("clearDepth");
            if (depth != null && !(bool)depth.GetValue(data)) return false;
            _stack = dataType.GetProperty("cameraStack")?.GetValue(mainData) as IList;
            if (_stack == null) return false;
            if (!_stack.Contains(_weapon)) _stack.Add(_weapon);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[L4D] 무기 전용 카메라 대신 기본 표시를 사용합니다: " + exception.Message);
            return false;
        }
    }

    private static void Set(Type type, object target, string name, object value)
    {
        PropertyInfo property = type.GetProperty(name);
        if (property != null && property.CanWrite) property.SetValue(target, value);
    }

    private void ExcludeOtherCameras()
    {
        if (_layer < 0) return;
        int bit = 1 << _layer;
        foreach (Camera camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (camera == _weapon) continue;
            int removed = camera.cullingMask & bit;
            if (!_removedMasks.ContainsKey(camera)) _removedMasks.Add(camera, removed);
            else _removedMasks[camera] |= removed;
            camera.cullingMask &= ~bit;
        }
    }

    private void LateUpdate()
    {
        Synchronize();
        if (_weapon != null && Time.unscaledTime >= _scanAt)
        {
            _scanAt = Time.unscaledTime + 1f;
            ExcludeOtherCameras();
        }
    }

    private void BeforeRender(ScriptableRenderContext context, Camera camera)
    {
        if (camera == _world || camera == _weapon) Synchronize();
    }

    private void BeforeBuiltinRender(Camera camera)
    {
        if (camera == _world || camera == _weapon) Synchronize();
    }

    private void Synchronize()
    {
        if (_weapon == null || _world == null) return;
        _weapon.enabled = _world.enabled && _world.gameObject.activeInHierarchy;
        _weapon.fieldOfView = _world.fieldOfView;
        _weapon.aspect = _world.aspect;
        _weapon.rect = _world.rect;
        _weapon.targetTexture = _world.targetTexture;
        _weapon.targetDisplay = _world.targetDisplay;
        _weapon.depth = _world.depth + 1f;
        _weapon.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        _weapon.ResetProjectionMatrix();
        if (_fill != null) _fill.enabled = _weapon.enabled;
    }

    private void OnDestroy()
    {
        RenderPipelineManager.beginCameraRendering -= BeforeRender;
        Camera.onPreCull -= BeforeBuiltinRender;
        if (_stack != null && _weapon != null && _stack.Contains(_weapon)) _stack.Remove(_weapon);
        foreach (KeyValuePair<Camera, int> entry in _removedMasks)
            if (entry.Key != null) entry.Key.cullingMask |= entry.Value;
        foreach (KeyValuePair<GameObject, int> entry in _originalLayers)
            if (entry.Key != null) entry.Key.layer = entry.Value;
        if (_weapon != null) Destroy(_weapon.gameObject);
    }
}
