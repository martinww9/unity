using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TriviaUI : MonoBehaviour
{
    public static TriviaUI Instance;

    [Header("Referencias de UI")]
    [SerializeField] private GameObject _panelPrincipal;
    [SerializeField] private TMP_Text _preguntaText;
    [SerializeField] private TMP_Text[] _opcionesTexts; // Array de 4 textos
    [SerializeField] private TMP_Text _timerText;

    [HideInInspector] public int LastSelectedIndex = -1;

    private void Awake() => Instance = this;

    public void ShowQuestion(Question q)
    {
        _panelPrincipal.SetActive(true);
        _preguntaText.text = q.text;
        LastSelectedIndex = -1; // Resetear selección

        for (int i = 0; i < _opcionesTexts.Length; i++)
        {
            _opcionesTexts[i].text = q.options[i];
        }
    }

    public void Hide()
    {
        _panelPrincipal.SetActive(false);
    }

    public void UpdateTimer(float time)
    {
        _timerText.text = time.ToString("F1") + "s";
    }

    // Asignar los 4 botones en el Inspector (pasando 0, 1, 2, 3)
    public void OnOptionClicked(int index)
    {
        LastSelectedIndex = index;
        Debug.Log("Opción seleccionada localmente: " + index);
    }
}