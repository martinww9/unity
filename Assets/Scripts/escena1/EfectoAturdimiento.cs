using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class EfectoAturdimiento : MonoBehaviour
{
    [SerializeField] private Image imagenEspiral;
    [SerializeField] private float velocidadRotacion = 150f;
    [SerializeField] private float fadeDuration = 0.3f;

    private bool _wasStunned;
    private bool _visible;
    private Coroutine _fadeRoutine;
    private Color _baseColor = Color.white;

    private void Awake()
    {
        if (imagenEspiral == null)
            imagenEspiral = GetComponent<Image>();

        if (imagenEspiral == null)
            return;

        _baseColor = imagenEspiral.color;
        imagenEspiral.raycastTarget = false;
        SetAlpha(0f);
        if (imagenEspiral.gameObject != gameObject)
            imagenEspiral.gameObject.SetActive(false);
        _visible = false;
        _wasStunned = false;
    }

    private void Update()
    {
        if (imagenEspiral == null)
            return;

        if (Player.Local == null)
        {
            if (_wasStunned || _visible)
                ForceHide();
            return;
        }

        bool stunned = Player.Local.State == EPlayerState.Stunned;
        if (stunned != _wasStunned)
        {
            _wasStunned = stunned;
            if (stunned)
                Mostrar();
            else
                Ocultar();
        }

        if (_visible)
            imagenEspiral.transform.Rotate(Vector3.forward, velocidadRotacion * Time.deltaTime);
    }

    private void Mostrar()
    {
        imagenEspiral.gameObject.SetActive(true);
        _visible = true;
        StartFade(1f);
    }

    private void Ocultar()
    {
        StartFade(0f, () =>
        {
            if (imagenEspiral != null)
                imagenEspiral.gameObject.SetActive(false);
            _visible = false;
        });
    }

    private void ForceHide()
    {
        _wasStunned = false;
        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        if (imagenEspiral != null)
        {
            SetAlpha(0f);
            imagenEspiral.gameObject.SetActive(false);
        }

        _visible = false;
    }

    private void StartFade(float targetAlpha, Action onComplete = null)
    {
        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);

        _fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, onComplete));
    }

    private IEnumerator FadeRoutine(float targetAlpha, Action onComplete)
    {
        float duration = Mathf.Max(fadeDuration, 0.001f);
        float startAlpha = imagenEspiral.color.a;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }

        SetAlpha(targetAlpha);
        _fadeRoutine = null;
        onComplete?.Invoke();
    }

    private void SetAlpha(float alpha)
    {
        Color c = _baseColor;
        c.a = alpha;
        imagenEspiral.color = c;
    }
}
