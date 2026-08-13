using UnityEngine;

public class Gun : MonoBehaviour
{
    private Vector3 targetPos;
    [SerializeField] private float range = 100f;
    [SerializeField] private float damage = 100f;

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
        }
    }
}
