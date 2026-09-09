using UnityEngine;
using System.Collections;

public class Gun : MonoBehaviour
{
    private Vector3 targetPos;
    [SerializeField] private float range = 100f;
    [SerializeField] private float damage = 100f;

    [Header("Muzzle Flash (optional)")]
    [Tooltip("Light that pulses brighter each time the gun fires, like a muzzle flash.")]
    [SerializeField] private Light muzzleLight;
    [SerializeField] private float muzzleFlashIntensity = 8f;
    [SerializeField] private float muzzleFlashDuration = 0.08f;

    private float muzzleBaseIntensity;
    private Coroutine muzzleRoutine;

    private void Start()
    {
        if (muzzleLight != null) muzzleBaseIntensity = muzzleLight.intensity;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Plane xyPlane = new Plane(Vector3.forward, new Vector3(0, 0, transform.position.z));
            float enter = 0f;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, LayerMask.GetMask("Enemy")))
            {
                targetPos = hit.point;
            }
            else if (xyPlane.Raycast(ray, out enter))
            {
                targetPos = ray.GetPoint(enter);
                targetPos.z = transform.position.z;
            }

            Debug.DrawLine(transform.position, targetPos, Color.green, 2f);

            Vector3 dir = (targetPos - transform.position).normalized;
            RaycastHit projHit;
            if (Physics.Raycast(transform.position, dir, out projHit, range, LayerMask.GetMask("Enemy", "Default")))
            {
                if (projHit.collider.gameObject.layer == LayerMask.NameToLayer("Enemy"))
                {
                    Health health = projHit.collider.gameObject.GetComponent<Health>();
                    health.Damage(damage);
                }
            }

            Fire();
        }
    }

    private void Fire()
    {
        if (muzzleLight == null) return;
        if (muzzleRoutine != null) StopCoroutine(muzzleRoutine);
        muzzleRoutine = StartCoroutine(MuzzleFlashRoutine());
    }

    private IEnumerator MuzzleFlashRoutine()
    {
        muzzleLight.intensity = muzzleFlashIntensity;

        float elapsedTime = 0f;
        while (elapsedTime < muzzleFlashDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / muzzleFlashDuration);
            muzzleLight.intensity = Mathf.Lerp(muzzleFlashIntensity, muzzleBaseIntensity, t);
            yield return null;
        }

        muzzleLight.intensity = muzzleBaseIntensity;
    }
}
