using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    [Header("Shake Settings")]
    [SerializeField, Range(0.01f, 1f)] private float defaultDuration = 0.12f;
    [SerializeField, Range(0.01f, 1f)] private float defaultMagnitude = 0.12f;

    private Coroutine activeShake;
    private Vector3 baseLocalPosition;

    private void Awake()
    {
        baseLocalPosition = transform.localPosition;
    }

    public void Shake(float duration = -1f, float magnitude = -1f)
    {
        float shakeDuration = duration > 0f ? duration : defaultDuration;
        float shakeMagnitude = magnitude > 0f ? magnitude : defaultMagnitude;

        if (activeShake != null)
            StopCoroutine(activeShake);

        activeShake = StartCoroutine(ShakeRoutine(shakeDuration, shakeMagnitude));
    }

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float damping = 1f - Mathf.Clamp01(elapsed / duration);
            Vector2 offset = Random.insideUnitCircle * (magnitude * damping);
            transform.localPosition = baseLocalPosition + new Vector3(offset.x, offset.y, 0f);
            yield return null;
        }

        transform.localPosition = baseLocalPosition;
        activeShake = null;
    }
}