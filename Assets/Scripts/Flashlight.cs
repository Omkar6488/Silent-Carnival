using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class Flashlight : MonoBehaviour
{
    #region Parameters

    [Header("Parameters")]
    public int maxBatteries = 4;
    public int currentBatteries = 4;

    public bool useBatteryLife = true;
    public bool useUi = true;
    public float drainSpeed = 1f;

    public bool recharge = true;
    public float rechargeSpeed = 0.5f;

    [Range(0, 1)]
    [Tooltip("Minimum battery percentage needed to turn flashlight back on")]
    public float minChargePercentage = 0.05f;

    public const float minBatteryCharge = 0f;
    public float maxBatteryCharge = 10f;

    public float followSpeed = 5f;
    public Quaternion offset = Quaternion.identity;

    #endregion

    #region References

    [Header("References")]
    public AudioClip onClip;
    public AudioClip offClip;
    public AudioClip newBatteryClip;
    public AudioMixerGroup sfxMixer;

    public Image stateImage;
    public Slider batteryChargeSlider;
    public Image batteryChargeSliderFill;
    public Text newBatteryText;
    public Text batteryCountText;
    public CanvasGroup holder;

    public Color fullChargeColor = Color.green;
    public Color noChargeColor = Color.red;

    public Camera mainCamera;
    public GameObject flashlight;

    #endregion

    #region Stats

    [Header("Stats")]
    public float batteryLife;
    public bool flashlightOn = false;
    private bool batteryDead = false;

    #endregion

    #region Private

    private IEnumerator batteryCoroutine;
    private Light spotLight;
    private float defaultIntensity;
    private AudioSource audioSource;

    #endregion

    void Start()
    {
        Init();
    }

    void Update()
    {
        // Toggle flashlight
        if (Input.GetKeyDown(KeyCode.F))
            ToggleFlashLight(!flashlightOn, true);

        // Reload battery
        if (Input.GetKeyDown(KeyCode.R) && CanReload())
            Reload();

        // Follow camera rotation
        if (flashlightOn && mainCamera)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                mainCamera.transform.rotation * offset,
                followSpeed * Time.deltaTime
            );
        }
    }

    #region Core Logic

    void ToggleFlashLight(bool state, bool playSound)
    {
        flashlightOn = state;

        bool canTurnOn = flashlightOn && !batteryDead;

        if (flashlight)
            flashlight.SetActive(flashlightOn);

        ToggleLight(canTurnOn);

        if (useUi && holder)
            holder.alpha = flashlightOn ? 1f : 0f;

        if (playSound)
            PlaySFX(flashlightOn ? onClip : offClip);

        UpdateBatteryProcess();
    }

    void Reload()
    {
        batteryLife = maxBatteryCharge;
        batteryDead = false;
        currentBatteries--;

        spotLight.intensity = GetLightIntensity();

        UpdateBatteryState(false);
        UpdateCountText();
        UpdateSlider();
        UpdateBatteryProcess();

        PlaySFX(newBatteryClip);
    }

    void UpdateBatteryProcess()
    {
        if (batteryCoroutine != null)
            StopCoroutine(batteryCoroutine);

        if (flashlightOn && !batteryDead && useBatteryLife)
        {
            batteryCoroutine = DrainBattery();
            StartCoroutine(batteryCoroutine);
        }
        else if (recharge)
        {
            batteryCoroutine = RechargeBattery();
            StartCoroutine(batteryCoroutine);
        }
    }

    IEnumerator DrainBattery()
    {
        while (batteryLife > minBatteryCharge)
        {
            batteryLife -= drainSpeed * Time.deltaTime;
            batteryLife = Mathf.Clamp(batteryLife, minBatteryCharge, maxBatteryCharge);

            spotLight.intensity = GetLightIntensity();
            UpdateSlider();

            yield return null;
        }

        UpdateBatteryState(true);
        UpdateBatteryProcess();
    }

    IEnumerator RechargeBattery()
    {
        while (batteryLife < maxBatteryCharge)
        {
            batteryLife += rechargeSpeed * Time.deltaTime;
            batteryLife = Mathf.Clamp(batteryLife, minBatteryCharge, maxBatteryCharge);

            spotLight.intensity = GetLightIntensity();
            UpdateSlider();

            if (ReloadReady() && batteryDead)
            {
                UpdateBatteryState(false);
                yield break;
            }

            yield return null;
        }
    }

    #endregion

    #region Helpers

    void UpdateBatteryState(bool dead)
    {
        batteryDead = dead;
        ToggleLight(!dead && flashlightOn);

        if (newBatteryText)
            newBatteryText.enabled = dead;

        if (stateImage)
            stateImage.color = dead ? new Color(1, 1, 1, 0.5f) : Color.white;
    }

    void ToggleLight(bool state)
    {
        if (spotLight)
            spotLight.enabled = state;
    }

    float GetLightIntensity()
    {
        return defaultIntensity * (batteryLife / maxBatteryCharge);
    }

    bool CanReload()
    {
        return flashlightOn && currentBatteries > 0 && batteryLife < maxBatteryCharge;
    }

    bool ReloadReady()
    {
        return (batteryLife / maxBatteryCharge) >= minChargePercentage;
    }

    void UpdateSlider()
    {
        if (!batteryChargeSlider) return;

        batteryChargeSlider.value = batteryLife;

        if (batteryChargeSliderFill)
            batteryChargeSliderFill.color =
                Color.Lerp(noChargeColor, fullChargeColor, batteryLife / maxBatteryCharge);
    }

    void UpdateCountText()
    {
        if (!batteryCountText) return;

        batteryCountText.text = $"Batteries: {currentBatteries} / {maxBatteries}";
    }

    void PlaySFX(AudioClip clip)
    {
        if (!clip || !audioSource) return;

        audioSource.clip = clip;
        audioSource.Play();
    }

    #endregion

    #region Init

    void Init()
    {
        // Light
        spotLight = GetComponent<Light>();
        if (!spotLight)
            spotLight = gameObject.AddComponent<Light>();

        spotLight.type = LightType.Spot;
        defaultIntensity = spotLight.intensity > 0 ? spotLight.intensity : 3f;

        // Audio
        audioSource = GetComponent<AudioSource>();
        if (!audioSource)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.outputAudioMixerGroup = sfxMixer;

        // Camera
        if (!mainCamera)
            mainCamera = Camera.main;

        // Battery
        batteryLife = maxBatteryCharge;
        UpdateBatteryState(false);

        // UI
        if (useUi && batteryChargeSlider)
        {
            batteryChargeSlider.minValue = minBatteryCharge;
            batteryChargeSlider.maxValue = maxBatteryCharge;
            UpdateSlider();

            if (newBatteryText)
                newBatteryText.text = "RELOAD (R)";

            UpdateCountText();
        }

        if (holder)
            holder.alpha = 0f;

        ToggleFlashLight(true, false);
    }

    #endregion
}
