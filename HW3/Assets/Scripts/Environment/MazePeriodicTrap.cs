using System.Collections;
using UnityEngine;

public enum MazeTrapType
{
    FloorFire,
    Arrow
}

// Runs a repeating coroutine that opens a short danger window for each trap.
public class MazePeriodicTrap : MonoBehaviour
{
    private MazeTrapType trapType;
    private float safeDuration;
    private float dangerDuration;
    private float animationLeadTime;
    private Vector3 trapScale;
    private Vector3 baseVisualScale;
    private Vector3 arrowDirection;
    private float arrowTravelDistance;
    private GameObject arrowProjectilePrefab;
    private GameObject fireVfxPrefab;
    private Material trapMaterial;
    private Light fireLight;
    private ParticleSystem[] fireParticles;
    private BoxCollider dangerTrigger;
    private bool initialized;
    private Coroutine cycleRoutine;

    public void Initialize(MazeTrapType type, float cellSize, float floorHeight,
        Vector3 shotDirection, float projectileTravelDistance,
        GameObject projectilePrefab, GameObject floorFireVfxPrefab,
        Material projectileMaterial,
        float safeTime, float dangerTime, float leadTime)
    {
        trapType = type;
        safeDuration = safeTime;
        dangerDuration = dangerTime;
        animationLeadTime = leadTime;
        arrowDirection = shotDirection;
        arrowTravelDistance = projectileTravelDistance;
        arrowProjectilePrefab = projectilePrefab;
        fireVfxPrefab = floorFireVfxPrefab;
        trapMaterial = projectileMaterial;
        trapScale = transform.lossyScale;
        baseVisualScale = transform.localScale;
        trapScale.x = Mathf.Max(Mathf.Abs(trapScale.x), 0.01f);
        trapScale.y = Mathf.Max(Mathf.Abs(trapScale.y), 0.01f);
        trapScale.z = Mathf.Max(Mathf.Abs(trapScale.z), 0.01f);

        dangerTrigger = GetComponent<BoxCollider>();
        if (dangerTrigger == null) dangerTrigger = gameObject.AddComponent<BoxCollider>();
        dangerTrigger.isTrigger = true;
        dangerTrigger.size = new Vector3(
            cellSize * 0.9f / trapScale.x,
            2.2f / trapScale.y,
            cellSize * 0.9f / trapScale.z);

        float prefabRootHeight = transform.position.y - floorHeight;
        dangerTrigger.center = new Vector3(0f,
            (0.9f - prefabRootHeight) / trapScale.y, 0f);
        dangerTrigger.enabled = false;

        if (trapType == MazeTrapType.FloorFire)
        {
            CreateFireVfx();
            fireLight = new GameObject("Fire Trap Light").AddComponent<Light>();
            fireLight.transform.SetParent(transform, false);
            fireLight.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.28f, 0.06f);
            fireLight.range = 4f;
            fireLight.intensity = 0.15f;
            transform.localScale = baseVisualScale * 0.9f;
        }

        Rigidbody trapBody = GetComponent<Rigidbody>();
        if (trapBody == null) trapBody = gameObject.AddComponent<Rigidbody>();
        trapBody.isKinematic = true;
        trapBody.useGravity = false;

        // Trap prefabs in the dungeon pack use non-convex mesh colliders. They cannot
        // be used on a Rigidbody, so keep the meshes visual and use the box trigger above.
        MeshCollider[] meshColliders = GetComponentsInChildren<MeshCollider>(true);
        foreach (MeshCollider meshCollider in meshColliders)
            meshCollider.enabled = false;

        initialized = true;
    }

    private void OnEnable()
    {
        if (initialized && cycleRoutine == null)
            cycleRoutine = StartCoroutine(RunTrapCycle());
    }

    private void OnDisable()
    {
        if (cycleRoutine != null)
        {
            StopCoroutine(cycleRoutine);
            cycleRoutine = null;
        }
    }

    private IEnumerator RunTrapCycle()
    {
        float stagger = Mathf.Abs(GetInstanceID() % 10) * 0.12f;
        yield return new WaitForSeconds(stagger);

        while (!MazeRoundUI.IsRoundEnding)
        {
            yield return new WaitForSeconds(safeDuration);
            if (MazeRoundUI.IsRoundEnding) break;

            yield return new WaitForSeconds(animationLeadTime);
            if (trapType == MazeTrapType.FloorFire)
            {
                dangerTrigger.enabled = true;
                yield return AnimateFire(true, dangerDuration);
                dangerTrigger.enabled = false;
                yield return AnimateFire(false, animationLeadTime);
            }
            else
            {
                LaunchArrow();
                yield return new WaitForSeconds(dangerDuration);
            }
        }
    }

    private IEnumerator AnimateFire(bool active, float duration)
    {
        if (fireParticles != null)
        {
            foreach (ParticleSystem particles in fireParticles)
            {
                if (active) particles.Play(false);
                else particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        Vector3 startScale = transform.localScale;
        Vector3 targetScale = baseVisualScale * (active ? 1.08f : 0.9f);
        float startIntensity = fireLight == null ? 0f : fireLight.intensity;
        float targetIntensity = active ? 2.4f : 0.15f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(duration, 0.01f));
            float pulse = active ? 1f + Mathf.Sin(elapsed * 18f) * 0.035f : 1f;
            transform.localScale = Vector3.Lerp(startScale, targetScale, t) * pulse;
            if (fireLight != null)
                fireLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, t) *
                    (active ? 0.82f + Mathf.Sin(elapsed * 22f) * 0.18f : 1f);
            yield return null;
        }

        transform.localScale = targetScale;
        if (fireLight != null) fireLight.intensity = targetIntensity;
    }

    private void CreateFireVfx()
    {
        if (fireVfxPrefab == null) return;

        GameObject effect = Instantiate(fireVfxPrefab, transform);
        effect.name = "Floor Fire VFX";
        effect.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        effect.transform.localRotation = Quaternion.identity;
        effect.transform.localScale = Vector3.one * 1.5f;

        fireParticles = effect.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem particles in fireParticles)
        {
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        foreach (AudioSource audioSource in effect.GetComponentsInChildren<AudioSource>(true))
            audioSource.enabled = false;
    }

    private void LaunchArrow()
    {
        if (arrowProjectilePrefab == null) return;

        Vector3 launchPosition = transform.position + arrowDirection * 0.35f;
        GameObject projectile = Instantiate(arrowProjectilePrefab, launchPosition,
            Quaternion.LookRotation(arrowDirection, Vector3.up));
        projectile.transform.localScale = Vector3.one * (1.4f / 0.5733f);

        Renderer projectileRenderer = projectile.GetComponent<Renderer>();
        if (projectileRenderer != null && trapMaterial != null)
            projectileRenderer.sharedMaterial = trapMaterial;

        BoxCollider projectileCollider = projectile.GetComponent<BoxCollider>();
        if (projectileCollider == null) projectileCollider = projectile.AddComponent<BoxCollider>();
        projectileCollider.isTrigger = true;

        Rigidbody projectileBody = projectile.GetComponent<Rigidbody>();
        if (projectileBody == null) projectileBody = projectile.AddComponent<Rigidbody>();
        projectileBody.isKinematic = true;
        projectileBody.useGravity = false;
        projectileBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        MazeTrapProjectile mover = projectile.AddComponent<MazeTrapProjectile>();
        mover.Launch(arrowDirection, 14f, arrowTravelDistance);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!dangerTrigger.enabled || MazeRoundUI.IsRoundEnding) return;

        Transform player = other.transform;
        while (player != null && !player.CompareTag("Player"))
            player = player.parent;

        if (player != null)
            MazeRoundUI.KillPlayer(player, GetDeathMessage());
    }

    private string GetDeathMessage()
    {
        if (trapType == MazeTrapType.FloorFire)
            return "The floor fire trap burned you.";
        return "The arrow trap hit you.";
    }
}

public class MazeTrapProjectile : MonoBehaviour
{
    private Vector3 direction;
    private float speed;
    private float remainingDistance;
    private bool launched;

    public void Launch(Vector3 travelDirection, float travelSpeed, float distance)
    {
        direction = travelDirection.normalized;
        speed = travelSpeed;
        remainingDistance = distance;
        launched = true;
    }

    private void Update()
    {
        if (!launched) return;

        float step = speed * Time.deltaTime;
        transform.position += direction * step;
        remainingDistance -= step;
        if (remainingDistance <= 0f) Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        Transform player = other.transform;
        while (player != null && !player.CompareTag("Player"))
            player = player.parent;

        if (player == null || MazeRoundUI.IsRoundEnding) return;
        MazeRoundUI.KillPlayer(player, "An arrow trap hit you.");
        Destroy(gameObject);
    }
}
