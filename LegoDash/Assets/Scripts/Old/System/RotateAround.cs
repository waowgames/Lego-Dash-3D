using UnityEngine;

public class RotateAround : MonoBehaviour
{
    [SerializeField] private float rotateSpeed = 100f;
    [SerializeField] private int rotateAxis; // 0 = X, 1 = Y, 2 = Z

    private void Update()
    {
        // Vector3.zero oluşturup sadece seçili ekseni dolduruyoruz
        Vector3 rotationVector = Vector3.zero;

        if (rotateAxis == 0) rotationVector.x = rotateSpeed;
        else if (rotateAxis == 1) rotationVector.y = rotateSpeed;
        else if (rotateAxis == 2) rotationVector.z = rotateSpeed;

        // Time.deltaTime ile çarpmak, dönüşü "saniye başına derece" yapar
        transform.Rotate(rotationVector * Time.deltaTime);
    }
}