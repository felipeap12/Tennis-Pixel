using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Gerenciador central de áudio do jogo (Música Tema em Looping e Efeitos Sonoros).
/// Suporta carregamento automático de MP3 em runtime e síntese procedural de efeitos retrô.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Música Tema de Fundo (Looping)")]
    [Tooltip("Clipe da música tema. Se deixado nulo, busca automaticamente o arquivo 'backgroundMusic.mp3'.")]
    public AudioClip backgroundMusicClip;

    [Tooltip("Volume da música tema de fundo (0 a 1).")]
    [Range(0f, 1f)] public float musicVolume = 0.35f;

    [Tooltip("Inicia a reprodução da música tema automaticamente ao carregar.")]
    public bool autoPlayMusic = true;

    [Header("Efeito Sonoro da Batida da Raquete")]
    [Tooltip("Áudio customizado para o golpe da raquete. Se nulo, sintetiza o som retrô chiptune automaticamente.")]
    public AudioClip racketHitSound;

    [Tooltip("Volume padrão do efeito sonoro de batida (0 a 1).")]
    [Range(0f, 1f)] public float hitSoundVolume = 0.85f;

    [Header("Fontes de Áudio (Configuradas Automaticamente)")]
    public AudioSource musicSource;
    public AudioSource sfxSource;

    private static AudioClip cachedProceduralHitClip = null;
    private Coroutine duckCoroutine = null;
    private bool isMusicLoading = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        SetupAudioSources();
    }

    void Start()
    {
        if (autoPlayMusic)
        {
            PlayBackgroundMusic();
        }
    }

    /// <summary>
    /// Garante que exista uma instância ativa do AudioManager na cena.
    /// </summary>
    public static AudioManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        AudioManager existing = FindAnyObjectByType<AudioManager>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject go = new GameObject("AudioManager");
        Instance = go.AddComponent<AudioManager>();
        if (go.transform.parent == null)
        {
            DontDestroyOnLoad(go);
        }
        return Instance;
    }

    /// <summary>
    /// Inicializa e configura os AudioSources para música (loop) e efeitos sonoros (SFX)
    /// </summary>
    public void SetupAudioSources()
    {
        if (musicSource == null)
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            if (sources.Length > 0) musicSource = sources[0];
            if (sources.Length > 1) sfxSource = sources[1];

            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }

        // Configurações do canal de música
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f; // Som estéreo 2D
        musicSource.volume = musicVolume;

        // Configurações do canal de efeitos
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;
        sfxSource.volume = hitSoundVolume;
    }

    #region Música Tema de Fundo (Looping)

    /// <summary>
    /// Inicia a reprodução contínua da música tema em looping
    /// </summary>
    public void PlayBackgroundMusic()
    {
        SetupAudioSources();

        if (musicSource.isPlaying && musicSource.clip != null)
        {
            return;
        }

        // 1. Se já tiver um clipe associado no Inspector
        if (backgroundMusicClip != null)
        {
            StartPlayingClip(backgroundMusicClip);
            return;
        }

        // 2. Tenta carregar a partir da pasta Resources
        AudioClip resClip = Resources.Load<AudioClip>("Audio/backgroundMusic");
        if (resClip == null) resClip = Resources.Load<AudioClip>("backgroundMusic");

        if (resClip != null)
        {
            backgroundMusicClip = resClip;
            StartPlayingClip(resClip);
            return;
        }

        // 3. Carrega dinamicamente a partir dos diretórios de arquivo
        if (!isMusicLoading)
        {
            StartCoroutine(LoadAndPlayBackgroundMusicRoutine());
        }
    }

    private void StartPlayingClip(AudioClip clip)
    {
        if (clip == null || musicSource == null) return;

        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.volume = musicVolume;
        musicSource.Play();
        Debug.Log($"[AudioManager] Música tema '{clip.name}' tocando em looping (Volume: {musicVolume * 100:F0}%).");
    }

    private IEnumerator LoadAndPlayBackgroundMusicRoutine()
    {
        isMusicLoading = true;

        string[] candidatePaths = new string[]
        {
            Path.Combine(Application.dataPath, "Audio", "backgroundMusic.mp3"),
            Path.Combine(Application.dataPath, "Resources", "Audio", "backgroundMusic.mp3"),
            @"C:\Users\WinUser\Downloads\teste\backgroundMusic.mp3"
        };

        string targetPath = null;
        foreach (var path in candidatePaths)
        {
            if (File.Exists(path))
            {
                targetPath = path;
                break;
            }
        }

        if (string.IsNullOrEmpty(targetPath))
        {
            Debug.LogWarning("[AudioManager] Arquivo 'backgroundMusic.mp3' não foi localizado nos caminhos de busca.");
            isMusicLoading = false;
            yield break;
        }

        string fileUrl = "file:///" + targetPath.Replace("\\", "/");
        Debug.Log($"[AudioManager] Carregando arquivo de áudio: {fileUrl}");

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(fileUrl, AudioType.MPEG))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip loadedClip = DownloadHandlerAudioClip.GetContent(www);
                if (loadedClip != null)
                {
                    loadedClip.name = "BackgroundTheme";
                    backgroundMusicClip = loadedClip;
                    StartPlayingClip(loadedClip);
                }
            }
            else
            {
                Debug.LogWarning($"[AudioManager] Erro ao carregar música via UnityWebRequest: {www.error}");
            }
        }

        isMusicLoading = false;
    }

    /// <summary>
    /// Pausa a música de fundo
    /// </summary>
    public void PauseBackgroundMusic()
    {
        if (musicSource != null && musicSource.isPlaying)
        {
            musicSource.Pause();
        }
    }

    /// <summary>
    /// Retoma a música de fundo pausada
    /// </summary>
    public void ResumeBackgroundMusic()
    {
        if (musicSource != null && !musicSource.isPlaying && musicSource.clip != null)
        {
            musicSource.UnPause();
        }
    }

    /// <summary>
    /// Interrompe a música de fundo
    /// </summary>
    public void StopBackgroundMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }

    /// <summary>
    /// Reduz gradualmente o volume da música (Ducking) para destacar fanfarras de vitória ou derrota
    /// </summary>
    public void DuckMusic(float targetVolume = 0.10f, float duration = 0.35f)
    {
        if (duckCoroutine != null) StopCoroutine(duckCoroutine);
        duckCoroutine = StartCoroutine(FadeMusicVolumeRoutine(targetVolume, duration));
    }

    /// <summary>
    /// Restaura o volume padrão da música tema
    /// </summary>
    public void RestoreMusic(float duration = 0.40f)
    {
        if (duckCoroutine != null) StopCoroutine(duckCoroutine);
        duckCoroutine = StartCoroutine(FadeMusicVolumeRoutine(musicVolume, duration));
    }

    private IEnumerator FadeMusicVolumeRoutine(float targetVol, float duration)
    {
        if (musicSource == null) yield break;

        float startVol = musicSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            musicSource.volume = Mathf.Lerp(startVol, targetVol, t);
            yield return null;
        }

        musicSource.volume = targetVol;
        duckCoroutine = null;
    }

    #endregion

    #region Efeito Sonoro da Batida da Raquete (SFX)

    /// <summary>
    /// Reproduz o efeito sonoro do impacto da raquete na bola com variação orgânica de tom (pitch)
    /// </summary>
    public void PlayRacketHit(float pitchOffset = 0f, float volumeMultiplier = 1f)
    {
        SetupAudioSources();

        AudioClip clip = racketHitSound != null ? racketHitSound : GetOrCreateRetroRacketHitSound();
        if (clip == null || sfxSource == null) return;

        // Variação de afinação aleatória (+/- 4%) para evitar efeito repetitivo e robótico
        float randomPitch = Random.Range(-0.04f, 0.04f);
        sfxSource.pitch = Mathf.Clamp(1.0f + pitchOffset + randomPitch, 0.7f, 1.4f);
        sfxSource.PlayOneShot(clip, hitSoundVolume * volumeMultiplier);
    }

    /// <summary>
    /// Obtém o clipe de som da batida da raquete (configurado ou sintetizado proceduralmente)
    /// </summary>
    public AudioClip GetRacketHitClip()
    {
        return racketHitSound != null ? racketHitSound : GetOrCreateRetroRacketHitSound();
    }

    /// <summary>
    /// Síntese procedural em runtime de um efeito sonoro de impacto de raquete retrô chiptune (Pop / Thwack).
    /// Combina ataque inicial agudo com transiente de ruído das cordas e decaimento acústico encorpado.
    /// </summary>
    public static AudioClip GetOrCreateRetroRacketHitSound()
    {
        if (cachedProceduralHitClip != null) return cachedProceduralHitClip;

        int sampleRate = 44100;
        float duration = 0.09f; // 90 milissegundos
        int totalSamples = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[totalSamples];

        float startFreq = 460f; // Impacto inicial nas cordas
        float endFreq = 140f;   // Ressonância acústica de fundo
        float phase = 0f;

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;

            // Transição rápida de tom simulando compressão da bola e vibração da raquete
            float currentFreq = Mathf.Lerp(startFreq, endFreq, Mathf.Pow(progress, 0.35f));
            phase += 2f * Mathf.PI * currentFreq * (1f / sampleRate);

            // Envelope exponencial ágil
            float env = Mathf.Exp(-t * 36f);

            // Transiente de ruído inicial simulando o atrito das cordas (primeiros 12ms)
            float noiseEnv = Mathf.Exp(-t * 85f);
            float noise = (Random.value * 2f - 1f) * noiseEnv * 0.32f;

            // Onda senoidal fundamental com saturação harmônica retrô
            float sine = Mathf.Sin(phase);
            float wave = Mathf.Clamp(sine * 1.25f, -1f, 1f) * 0.70f + noise;

            samples[i] = wave * env * 0.85f;
        }

        cachedProceduralHitClip = AudioClip.Create("RacketHitRetro", totalSamples, 1, sampleRate, false);
        cachedProceduralHitClip.SetData(samples, 0);
        return cachedProceduralHitClip;
    }

    #endregion
}
