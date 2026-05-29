using UnityEngine;

/// <summary>
/// 오디오 매니저 싱글턴
/// BGM 재생 및 비트 싱크, SFX 원샷 재생을 중앙에서 관리
/// 씬의 빈 GameObject에 부착하거나 자동 생성
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource _bgmSource;
    [SerializeField] private AudioSource _sfxSource;

    [Header("BGM 설정")]
    [SerializeField] [Range(0f, 1f)] private float _bgmVolume = 0.7f;

    [Header("SFX 설정")]
    [SerializeField] [Range(0f, 1f)] private float _sfxVolume = 1f;

    [Header("SFX 클립 (추후 할당)")]
    [Tooltip("색칠 시 효과음")]
    [SerializeField] private AudioClip _paintSfx;
    
    [Tooltip("판정 성공 효과음 (Perfect/Great/Good)")]
    [SerializeField] private AudioClip _judgmentGoodSfx;
    
    [Tooltip("판정 실패 효과음 (Miss)")]
    [SerializeField] private AudioClip _judgmentMissSfx;

    /// <summary>현재 BGM 재생 시간 (초) — 비트 싱크에 사용</summary>
    public float CurrentBGMTime => _bgmSource != null && _bgmSource.isPlaying ? _bgmSource.time : 0f;

    /// <summary>BGM 재생 중 여부</summary>
    public bool IsBGMPlaying => _bgmSource != null && _bgmSource.isPlaying;

    private void Awake()
    {
        // 싱글턴 설정
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // AudioSource 자동 생성 (없을 경우)
        if (_bgmSource == null)
        {
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.playOnAwake = false;
            _bgmSource.loop = false;
        }
        if (_sfxSource == null)
        {
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.loop = false;
        }

        _bgmSource.volume = _bgmVolume;
        _sfxSource.volume = _sfxVolume;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    #region BGM

    /// <summary>
    /// BGM 재생 시작
    /// </summary>
    /// <param name="clip">재생할 오디오 클립</param>
    /// <param name="startOffset">재생 시작 위치 (초)</param>
    public void PlayBGM(AudioClip clip, float startOffset = 0f)
    {
        if (_bgmSource == null || clip == null) return;

        _bgmSource.clip = clip;
        _bgmSource.volume = _bgmVolume;
        _bgmSource.loop = false;
        _bgmSource.time = Mathf.Clamp(startOffset, 0f, clip.length);
        _bgmSource.Play();
        
        Debug.Log($"[AudioManager] BGM 재생: {clip.name}, offset: {startOffset}s");
    }

    /// <summary>
    /// BGM 정지
    /// </summary>
    public void StopBGM()
    {
        if (_bgmSource == null) return;
        _bgmSource.Stop();
    }

    /// <summary>
    /// BGM 일시정지
    /// </summary>
    public void PauseBGM()
    {
        if (_bgmSource == null) return;
        _bgmSource.Pause();
    }

    /// <summary>
    /// BGM 재개
    /// </summary>
    public void ResumeBGM()
    {
        if (_bgmSource == null) return;
        _bgmSource.UnPause();
    }

    #endregion

    #region SFX

    /// <summary>
    /// SFX 원샷 재생
    /// </summary>
    public void PlaySFX(AudioClip clip)
    {
        if (_sfxSource == null || clip == null) return;
        _sfxSource.PlayOneShot(clip, _sfxVolume);
    }

    /// <summary>
    /// 색칠 효과음 재생
    /// </summary>
    public void PlayPaintSFX()
    {
        if (_paintSfx != null)
            PlaySFX(_paintSfx);
    }

    /// <summary>
    /// 판정 등급에 따른 효과음 재생
    /// </summary>
    public void PlayJudgmentSFX(JudgmentGrade grade)
    {
        switch (grade)
        {
            case JudgmentGrade.Perfect:
            case JudgmentGrade.Great:
            case JudgmentGrade.Good:
                if (_judgmentGoodSfx != null)
                    PlaySFX(_judgmentGoodSfx);
                break;
            case JudgmentGrade.Miss:
                if (_judgmentMissSfx != null)
                    PlaySFX(_judgmentMissSfx);
                break;
        }
    }

    #endregion

    #region Volume Control

    /// <summary>BGM 볼륨 설정 (0~1)</summary>
    public void SetBGMVolume(float volume)
    {
        _bgmVolume = Mathf.Clamp01(volume);
        if (_bgmSource != null)
            _bgmSource.volume = _bgmVolume;
    }

    /// <summary>SFX 볼륨 설정 (0~1)</summary>
    public void SetSFXVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
        if (_sfxSource != null)
            _sfxSource.volume = _sfxVolume;
    }

    #endregion
}
