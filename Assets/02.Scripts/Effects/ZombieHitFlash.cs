using UnityEngine;

// 총에 맞은 좀비가 아주 잠깐 하얗게(헤드샷은 노랗게) 번쩍인다.
// 재질은 바꾸지 않고 MaterialPropertyBlock으로 잠깐 덮어쓴다. (특수 좀비 색도 그대로 유지)
public class ZombieHitFlash : MonoBehaviour
{
    private Renderer[] _renderers;
    private MaterialPropertyBlock _block;
    private float _timer;
    private float _duration = 0.08f;
    private Color _color;

    public static void Flash(GameObject zombie, bool headshot)
    {
        ZombieHitFlash flash = zombie.GetComponent<ZombieHitFlash>();
        if (flash == null)
        {
            flash = zombie.AddComponent<ZombieHitFlash>();
        }

        flash.Begin(headshot ? new Color(1f, 0.85f, 0.4f) : new Color(1f, 0.55f, 0.5f));
    }

    private void Begin(Color color)
    {
        if (_renderers == null)
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _block = new MaterialPropertyBlock();
        }

        _color = color;
        _timer = _duration;
        Apply(1f);
    }

    private void Update()
    {
        if (_timer <= 0f)
        {
            return;
        }

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            Clear();
        }
        else
        {
            Apply(_timer / _duration);
        }
    }

    private void Apply(float strength)
    {
        foreach (Renderer renderer in _renderers)
        {
            if (renderer == null || !(renderer is SkinnedMeshRenderer || renderer is MeshRenderer))
            {
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null || !material.HasProperty("_BaseColor"))
                {
                    continue;
                }

                // 원래 색을 밝게 끌어올린다. (1보다 크면 더 밝게 보인다)
                Color baseColor = material.GetColor("_BaseColor");
                Color flash = Color.Lerp(baseColor, _color * 3f, strength);
                renderer.GetPropertyBlock(_block, i);
                _block.SetColor("_BaseColor", flash);
                renderer.SetPropertyBlock(_block, i);
            }
        }
    }

    private void Clear()
    {
        foreach (Renderer renderer in _renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            // 빈 블록을 넣으면 원래 재질 색으로 돌아간다.
            _block.Clear();
            int count = renderer.sharedMaterials.Length;
            for (int i = 0; i < count; i++)
            {
                renderer.SetPropertyBlock(_block, i);
            }
        }
    }
}
