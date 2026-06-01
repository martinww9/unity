using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Fusion;

public class QuestionManager : NetworkBehaviour
{
    public static QuestionManager Instance;

    private const int LevelCount = 3;
    private static readonly int[] AllLevels = { 1, 2, 3 };
    private const int MaxFieldCharsPerRpc = 400;

    private const int FieldId = 0;
    private const int FieldQuestion = 1;
    private const int FieldOption0 = 2;
    private const int FieldOption1 = 3;
    private const int FieldOption2 = 4;
    private const int FieldOption3 = 5;

    private readonly Dictionary<int, Question[]> _questionsByLevel = new Dictionary<int, Question[]>();
    private readonly Dictionary<int, List<Question>> _incomingByLevel = new Dictionary<int, List<Question>>();
    private readonly Dictionary<int, Dictionary<int, Question>> _completedByIndexByLevel = new Dictionary<int, Dictionary<int, Question>>();
    private readonly Dictionary<int, int> _expectedCountByLevel = new Dictionary<int, int>();
    private readonly Dictionary<(int nivel, int qIndex), PartialQuestion> _partialQuestions = new Dictionary<(int, int), PartialQuestion>();
    private int _levelsReceived;

    public bool IsReady { get; private set; }

    private const string BASE_URL = "http://localhost:5000/api";
    private const string ENDPOINT_QUESTIONS_GENERATE = "/questions/generate";
    private const string ENDPOINT_QUESTIONS_STATUS = "/questions/status";
    private const string ENDPOINT_QUESTIONS_LEVEL = "/questions/";
    private const string ENDPOINT_FEEDBACK = "/feedback";
    private const float PollIntervalSeconds = 3f;

    private class PartialQuestion
    {
        public string id;
        public string question;
        public readonly string[] options = new string[4];
        public int correctAnswerIndex;
        public int puntaje = 10;
        public string dificultad = "Fácil";
        public bool metaReceived;
        public readonly HashSet<int> fieldsReceived = new HashSet<int>();
        public readonly Dictionary<int, ChunkAssembly> chunkAssemblies = new Dictionary<int, ChunkAssembly>();
    }

    private class ChunkAssembly
    {
        public int totalChunks;
        public readonly Dictionary<int, string> chunks = new Dictionary<int, string>();
    }

    private void Awake() => Instance = this;

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
            StartCoroutine(CheckExistingQuestions());
    }

    public void RetryConnection()
    {
        if (Object.HasStateAuthority)
            StartCoroutine(GenerateAndDownloadQuestions());
    }

    public void RequestNewGeneration()
    {
        if (Object.HasStateAuthority)
        {
            IsReady = false;
            _questionsByLevel.Clear();
            StartCoroutine(GenerateAndDownloadQuestions());
        }
    }

    public int GetMaxPossibleScore()
    {
        int total = 0;
        foreach (int level in AllLevels)
        {
            if (!_questionsByLevel.TryGetValue(level, out Question[] questions) || questions == null)
                continue;
            foreach (var q in questions)
                total += q.puntaje;
        }
        return total;
    }

    private static void ApplyNgrokHeaders(UnityWebRequest request)
    {
        request.SetRequestHeader("ngrok-skip-browser-warning", "69420");
    }

    private bool AllLevelsLoaded()
    {
        foreach (int level in AllLevels)
        {
            if (!_questionsByLevel.TryGetValue(level, out Question[] questions) || questions == null || questions.Length == 0)
                return false;
        }
        return true;
    }

    private void MarkReadyIfComplete()
    {
        if (!AllLevelsLoaded()) return;

        IsReady = true;
        Debug.Log("Trivia sincronizada (3 niveles).");
        if (TriviaUI.Instance != null)
            TriviaUI.Instance.OnQuestionsReady();
    }

    private static byte EncodeDificultad(string dificultad)
    {
        if (string.IsNullOrEmpty(dificultad)) return 0;
        if (dificultad.StartsWith("Med")) return 1;
        if (dificultad.StartsWith("Dif") || dificultad.StartsWith("dif")) return 2;
        return 0;
    }

    private static string DecodeDificultad(byte code)
    {
        switch (code)
        {
            case 1: return "Media";
            case 2: return "Difícil";
            default: return "Fácil";
        }
    }

    private PartialQuestion GetOrCreatePartial(int nivel, int qIndex)
    {
        var key = (nivel, qIndex);
        if (!_partialQuestions.TryGetValue(key, out PartialQuestion partial))
        {
            partial = new PartialQuestion();
            _partialQuestions[key] = partial;
        }
        return partial;
    }

    private void ApplyQuestionMeta(int nivel, int qIndex, int correct, int puntaje, byte dificultadCode)
    {
        PartialQuestion partial = GetOrCreatePartial(nivel, qIndex);
        partial.correctAnswerIndex = correct;
        partial.puntaje = puntaje == 0 ? 10 : puntaje;
        partial.dificultad = DecodeDificultad(dificultadCode);
        partial.metaReceived = true;
        TryCompleteQuestion(nivel, qIndex);
    }

    private void ApplyQuestionField(int nivel, int qIndex, int fieldId, string text)
    {
        PartialQuestion partial = GetOrCreatePartial(nivel, qIndex);
        text = text ?? "";

        switch (fieldId)
        {
            case FieldId: partial.id = text; break;
            case FieldQuestion: partial.question = text; break;
            case FieldOption0: partial.options[0] = text; break;
            case FieldOption1: partial.options[1] = text; break;
            case FieldOption2: partial.options[2] = text; break;
            case FieldOption3: partial.options[3] = text; break;
            default: return;
        }

        partial.fieldsReceived.Add(fieldId);
        TryCompleteQuestion(nivel, qIndex);
    }

    private void ApplyQuestionFieldChunk(int nivel, int qIndex, int fieldId, int chunkIndex, int totalChunks, string text)
    {
        PartialQuestion partial = GetOrCreatePartial(nivel, qIndex);
        if (!partial.chunkAssemblies.TryGetValue(fieldId, out ChunkAssembly assembly))
        {
            assembly = new ChunkAssembly { totalChunks = totalChunks };
            partial.chunkAssemblies[fieldId] = assembly;
        }

        assembly.chunks[chunkIndex] = text ?? "";

        if (assembly.chunks.Count != assembly.totalChunks)
            return;

        var sb = new StringBuilder();
        for (int i = 0; i < assembly.totalChunks; i++)
            sb.Append(assembly.chunks[i]);

        partial.chunkAssemblies.Remove(fieldId);
        ApplyQuestionField(nivel, qIndex, fieldId, sb.ToString());
    }

    private void TryCompleteQuestion(int nivel, int qIndex)
    {
        var key = (nivel, qIndex);
        if (!_partialQuestions.TryGetValue(key, out PartialQuestion partial))
            return;

        if (!partial.metaReceived) return;
        for (int fieldId = FieldId; fieldId <= FieldOption3; fieldId++)
        {
            if (!partial.fieldsReceived.Contains(fieldId))
                return;
        }

        if (!_incomingByLevel.TryGetValue(nivel, out _))
            return;

        Question q = new Question
        {
            id = string.IsNullOrEmpty(partial.id) ? $"N{nivel}_{qIndex + 1}" : partial.id,
            question = string.IsNullOrEmpty(partial.question) ? "Pregunta Corrupta" : partial.question,
            options = new string[]
            {
                partial.options[0] ?? "",
                partial.options[1] ?? "",
                partial.options[2] ?? "",
                partial.options[3] ?? ""
            },
            correctAnswerIndex = partial.correctAnswerIndex,
            dificultad = partial.dificultad,
            puntaje = partial.puntaje
        };

        _partialQuestions.Remove(key);

        if (!_completedByIndexByLevel.TryGetValue(nivel, out Dictionary<int, Question> byIndex))
        {
            byIndex = new Dictionary<int, Question>();
            _completedByIndexByLevel[nivel] = byIndex;
        }

        byIndex[qIndex] = q;

        if (!_expectedCountByLevel.TryGetValue(nivel, out int expected) || byIndex.Count != expected)
            return;

        var sorted = new Question[expected];
        for (int i = 0; i < expected; i++)
            sorted[i] = byIndex[i];

        _questionsByLevel[nivel] = sorted;
        _levelsReceived++;
        Debug.Log($"IA: Nivel {nivel} sincronizado.");

        if (_levelsReceived >= LevelCount)
            MarkReadyIfComplete();
    }

    private void SendQuestionFieldRpc(int nivel, int qIndex, int fieldId, string text)
    {
        text ??= "";
        if (text.Length <= MaxFieldCharsPerRpc)
        {
            RPC_SyncQuestionField(nivel, qIndex, fieldId, text);
            return;
        }

        int totalChunks = (text.Length + MaxFieldCharsPerRpc - 1) / MaxFieldCharsPerRpc;
        for (int i = 0; i < totalChunks; i++)
        {
            int start = i * MaxFieldCharsPerRpc;
            int len = Mathf.Min(MaxFieldCharsPerRpc, text.Length - start);
            RPC_SyncQuestionFieldChunk(nivel, qIndex, fieldId, i, totalChunks, text.Substring(start, len));
        }
    }

    private void SendQuestionFieldRpcTarget(PlayerRef target, int nivel, int qIndex, int fieldId, string text)
    {
        text ??= "";
        if (text.Length <= MaxFieldCharsPerRpc)
        {
            RPC_SyncQuestionFieldTarget(target, nivel, qIndex, fieldId, text);
            return;
        }

        int totalChunks = (text.Length + MaxFieldCharsPerRpc - 1) / MaxFieldCharsPerRpc;
        for (int i = 0; i < totalChunks; i++)
        {
            int start = i * MaxFieldCharsPerRpc;
            int len = Mathf.Min(MaxFieldCharsPerRpc, text.Length - start);
            RPC_SyncQuestionFieldChunkTarget(target, nivel, qIndex, fieldId, i, totalChunks, text.Substring(start, len));
        }
    }

    private void SendQuestionParts(int nivel, int qIndex, Question q)
    {
        RPC_SyncQuestionMeta(nivel, qIndex, q.correctAnswerIndex, q.puntaje, EncodeDificultad(q.dificultad));
        SendQuestionFieldRpc(nivel, qIndex, FieldId, q.id);
        SendQuestionFieldRpc(nivel, qIndex, FieldQuestion, q.question);

        string o1 = q.options != null && q.options.Length > 0 ? q.options[0] : "";
        string o2 = q.options != null && q.options.Length > 1 ? q.options[1] : "";
        string o3 = q.options != null && q.options.Length > 2 ? q.options[2] : "";
        string o4 = q.options != null && q.options.Length > 3 ? q.options[3] : "";

        SendQuestionFieldRpc(nivel, qIndex, FieldOption0, o1);
        SendQuestionFieldRpc(nivel, qIndex, FieldOption1, o2);
        SendQuestionFieldRpc(nivel, qIndex, FieldOption2, o3);
        SendQuestionFieldRpc(nivel, qIndex, FieldOption3, o4);
    }

    private void SendQuestionPartsTarget(PlayerRef target, int nivel, int qIndex, Question q)
    {
        RPC_SyncQuestionMetaTarget(target, nivel, qIndex, q.correctAnswerIndex, q.puntaje, EncodeDificultad(q.dificultad));
        SendQuestionFieldRpcTarget(target, nivel, qIndex, FieldId, q.id);
        SendQuestionFieldRpcTarget(target, nivel, qIndex, FieldQuestion, q.question);

        string o1 = q.options != null && q.options.Length > 0 ? q.options[0] : "";
        string o2 = q.options != null && q.options.Length > 1 ? q.options[1] : "";
        string o3 = q.options != null && q.options.Length > 2 ? q.options[2] : "";
        string o4 = q.options != null && q.options.Length > 3 ? q.options[3] : "";

        SendQuestionFieldRpcTarget(target, nivel, qIndex, FieldOption0, o1);
        SendQuestionFieldRpcTarget(target, nivel, qIndex, FieldOption1, o2);
        SendQuestionFieldRpcTarget(target, nivel, qIndex, FieldOption2, o3);
        SendQuestionFieldRpcTarget(target, nivel, qIndex, FieldOption3, o4);
    }

    private bool TryParseLevelPool(string json, out LevelQuestionPool pool)
    {
        pool = null;
        try
        {
            pool = JsonUtility.FromJson<LevelQuestionPool>(json);
            return pool != null;
        }
        catch (System.Exception e)
        {
            Debug.LogError("IA: Error parseando JSON de preguntas: " + e.Message);
            return false;
        }
    }

    private void SincronizarNivel(int nivel, Question[] questions)
    {
        if (questions == null || questions.Length == 0) return;

        RPC_StartSyncLevel(nivel, questions.Length);
        for (int i = 0; i < questions.Length; i++)
            SendQuestionParts(nivel, i, questions[i]);
    }

    private void SincronizarTodosLosNiveles(Dictionary<int, Question[]> byLevel)
    {
        RPC_StartSyncAllLevels();
        foreach (int level in AllLevels)
        {
            if (byLevel.TryGetValue(level, out Question[] questions))
                SincronizarNivel(level, questions);
        }
    }

    private IEnumerator DownloadAllLevels()
    {
        var downloaded = new Dictionary<int, Question[]>();

        foreach (int level in AllLevels)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(BASE_URL + ENDPOINT_QUESTIONS_LEVEL + level))
            {
                ApplyNgrokHeaders(request);
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"IA: Error descargando nivel {level}: {request.error}");
                    yield break;
                }

                if (!TryParseLevelPool(request.downloadHandler.text, out LevelQuestionPool pool))
                    yield break;

                if (pool.questions == null || pool.questions.Length == 0)
                    yield break;

                downloaded[level] = pool.questions;
            }
        }

        if (downloaded.Count == LevelCount)
            SincronizarTodosLosNiveles(downloaded);
    }

    IEnumerator CheckExistingQuestions()
    {
        Debug.Log("IA: Verificando preguntas existentes en el servidor...");

        using (UnityWebRequest statusRequest = UnityWebRequest.Get(BASE_URL + ENDPOINT_QUESTIONS_STATUS))
        {
            ApplyNgrokHeaders(statusRequest);
            yield return statusRequest.SendWebRequest();

            if (statusRequest.result != UnityWebRequest.Result.Success)
            {
                if (TriviaUI.Instance != null)
                {
                    TriviaUI.Instance.ShowGenerateButton();
                    TriviaUI.Instance.OnConnectionError();
                }
                yield break;
            }

            QuestionsStatusResponse status = JsonUtility.FromJson<QuestionsStatusResponse>(statusRequest.downloadHandler.text);
            if (status == null || status.status != "completed")
            {
                Debug.Log($"IA: Preguntas no disponibles (status={status?.status}). Esperando generación.");
                if (TriviaUI.Instance != null) TriviaUI.Instance.ShowGenerateButton();
                yield break;
            }
        }

        yield return DownloadAllLevels();
    }

    IEnumerator GenerateAndDownloadQuestions()
    {
        using (UnityWebRequest webRequest = new UnityWebRequest(BASE_URL + ENDPOINT_QUESTIONS_GENERATE, "POST"))
        {
            webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            ApplyNgrokHeaders(webRequest);

            yield return webRequest.SendWebRequest();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error al contactar a la IA: " + webRequest.error);
                if (TriviaUI.Instance != null) TriviaUI.Instance.OnConnectionError();
                yield break;
            }

            GenerateResponse generateResponse = JsonUtility.FromJson<GenerateResponse>(webRequest.downloadHandler.text);
            Debug.Log($"IA: {generateResponse?.message ?? "Generación solicitada."} (status={generateResponse?.status})");

            if (generateResponse != null && generateResponse.status == "indexing")
                Debug.LogWarning("IA: El servidor aún está indexando los documentos.");
        }

        bool finished = false;
        while (!finished)
        {
            yield return new WaitForSeconds(PollIntervalSeconds);

            using (UnityWebRequest statusRequest = UnityWebRequest.Get(BASE_URL + ENDPOINT_QUESTIONS_STATUS))
            {
                ApplyNgrokHeaders(statusRequest);
                yield return statusRequest.SendWebRequest();

                if (statusRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("IA: Error consultando estado: " + statusRequest.error);
                    continue;
                }

                QuestionsStatusResponse levelsStatus = JsonUtility.FromJson<QuestionsStatusResponse>(statusRequest.downloadHandler.text);
                Debug.Log($"IA: Estado global={levelsStatus?.status}");

                if (levelsStatus == null) continue;

                if (levelsStatus.status == "error")
                {
                    Debug.LogError("IA: Error generando preguntas en el servidor.");
                    if (TriviaUI.Instance != null) TriviaUI.Instance.OnConnectionError();
                    finished = true;
                    continue;
                }

                if (levelsStatus.status != "completed") continue;
            }

            yield return DownloadAllLevels();
            finished = IsReady;
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_StartSyncAllLevels()
    {
        _incomingByLevel.Clear();
        _completedByIndexByLevel.Clear();
        _expectedCountByLevel.Clear();
        _questionsByLevel.Clear();
        _partialQuestions.Clear();
        _levelsReceived = 0;
        IsReady = false;
        Debug.Log("IA: Iniciando recepción de preguntas por nivel...");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_StartSyncLevel(int nivel, int totalQuestions)
    {
        _incomingByLevel[nivel] = new List<Question>();
        _completedByIndexByLevel[nivel] = new Dictionary<int, Question>();
        _expectedCountByLevel[nivel] = totalQuestions;
        Debug.Log($"IA: Recibiendo nivel {nivel} ({totalQuestions} preguntas)...");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SyncQuestionMeta(int nivel, int qIndex, int correct, int puntaje, byte dificultadCode)
    {
        ApplyQuestionMeta(nivel, qIndex, correct, puntaje, dificultadCode);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SyncQuestionField(int nivel, int qIndex, int fieldId, string text)
    {
        ApplyQuestionField(nivel, qIndex, fieldId, text);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SyncQuestionFieldChunk(int nivel, int qIndex, int fieldId, int chunkIndex, int totalChunks, string text)
    {
        ApplyQuestionFieldChunk(nivel, qIndex, fieldId, chunkIndex, totalChunks, text);
    }

    public Question GetQuestion(int level, int index)
    {
        if (!_questionsByLevel.TryGetValue(level, out Question[] questions) || questions == null)
            return null;
        if (index < 0 || index >= questions.Length)
            return null;
        return questions[index];
    }

    public int GetQuestionCount(int level)
    {
        if (!_questionsByLevel.TryGetValue(level, out Question[] questions) || questions == null)
            return 0;
        return questions.Length;
    }

    public void SincronizarConNuevoJugador(PlayerRef nuevoJugador)
    {
        if (!Object.HasStateAuthority || !IsReady || !AllLevelsLoaded())
        {
            Debug.LogWarning("[QuestionManager] Preguntas no listas aún.");
            return;
        }

        if (nuevoJugador == Runner.LocalPlayer) return;

        Debug.Log($"[Host] Sincronizando trivia con el jugador: {nuevoJugador.PlayerId}");

        RPC_EnviarInicioATarget(nuevoJugador);

        foreach (int level in AllLevels)
        {
            if (!_questionsByLevel.TryGetValue(level, out Question[] questions))
                continue;

            RPC_EnviarInicioNivelATarget(nuevoJugador, level, questions.Length);

            for (int i = 0; i < questions.Length; i++)
                SendQuestionPartsTarget(nuevoJugador, level, i, questions[i]);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_EnviarInicioATarget([RpcTarget] PlayerRef target)
    {
        if (Object.HasStateAuthority) return;
        _incomingByLevel.Clear();
        _completedByIndexByLevel.Clear();
        _expectedCountByLevel.Clear();
        _questionsByLevel.Clear();
        _partialQuestions.Clear();
        _levelsReceived = 0;
        IsReady = false;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_EnviarInicioNivelATarget([RpcTarget] PlayerRef target, int nivel, int totalQuestions)
    {
        if (Object.HasStateAuthority) return;
        _incomingByLevel[nivel] = new List<Question>();
        _completedByIndexByLevel[nivel] = new Dictionary<int, Question>();
        _expectedCountByLevel[nivel] = totalQuestions;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SyncQuestionMetaTarget([RpcTarget] PlayerRef target, int nivel, int qIndex, int correct, int puntaje, byte dificultadCode)
    {
        if (Object.HasStateAuthority) return;
        ApplyQuestionMeta(nivel, qIndex, correct, puntaje, dificultadCode);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SyncQuestionFieldTarget([RpcTarget] PlayerRef target, int nivel, int qIndex, int fieldId, string text)
    {
        if (Object.HasStateAuthority) return;
        ApplyQuestionField(nivel, qIndex, fieldId, text);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SyncQuestionFieldChunkTarget([RpcTarget] PlayerRef target, int nivel, int qIndex, int fieldId, int chunkIndex, int totalChunks, string text)
    {
        if (Object.HasStateAuthority) return;
        ApplyQuestionFieldChunk(nivel, qIndex, fieldId, chunkIndex, totalChunks, text);
    }

    public void SolicitarFeedbackFinal(int score, int total, int nivel = 3)
    {
        StartCoroutine(PostFeedbackRoutine(score, total, nivel));
    }

    private IEnumerator PostFeedbackRoutine(int score, int total, int nivel)
    {
        string url = BASE_URL + ENDPOINT_FEEDBACK;
        FeedbackRequest payload = new FeedbackRequest { score = score, total = total, nivel = nivel };
        string jsonBody = JsonUtility.ToJson(payload);

        using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            ApplyNgrokHeaders(webRequest);
            webRequest.timeout = 300;

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                FeedbackData data = JsonUtility.FromJson<FeedbackData>(webRequest.downloadHandler.text);
                Debug.Log("IA: Feedback generado correctamente.");

                if (TriviaUI.Instance != null)
                    TriviaUI.Instance.ShowFeedback(data);
            }
            else
            {
                Debug.LogError("IA: Error al procesar la retroalimentación: " + webRequest.error);
                if (TriviaUI.Instance != null)
                    TriviaUI.Instance.OnFeedbackError();
            }
        }
    }
}
