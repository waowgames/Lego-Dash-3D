using System.Collections;
using UnityEngine;

public class RotateAround : MonoBehaviour
{
    [SerializeField] private float rotateSpeed;
    [SerializeField] private float rotateAxis; // 0 = X, 1 = Y, 2 = Z
    private Transform _transform;

    private void Start()
    {
        _transform = GetComponent<Transform>();
        StartCoroutine(RotateCoroutine());
    }

    private IEnumerator RotateCoroutine()
    {
        while (true)
        {
            RotateAroundUpdate();
            yield return new WaitForSeconds(0.02f); // Update every 20 milliseconds
        }
    }

    private void RotateAroundUpdate()
    {
        _transform.Rotate(
            rotateAxis == 0 ? 30 * Time.deltaTime * rotateSpeed : 0,
            rotateAxis == 1 ? 30 * Time.deltaTime * rotateSpeed : 0,
            rotateAxis == 2 ? 30 * Time.deltaTime * rotateSpeed : 0
        );
    }

}
