using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.XR.Management;
using Varjo.XR;

public class MRController : MonoBehaviour
{
    [Serializable]
    public class CubemapEvent : UnityEvent { }

    public Camera xrCamera;

    [Header("Mixed Reality Features")]
    private bool _videoSeeThrough = false;
    public bool VideoSeeThrough
    {
        get => _videoSeeThrough;
        set
        {
            if (_videoSeeThrough == value) return;
            _videoSeeThrough = value;

            if (_videoSeeThrough)
            {
                _videoSeeThrough = VarjoMixedReality.StartRender();
                if(!_videoSeeThrough) 
                {
                    Debug.LogError("Error Starting Mixed Reality Mode");
                    return;
                }

                if (HDCameraData)
                    HDCameraData.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            }
            else
            {
                VarjoMixedReality.StopRender();
                if (HDCameraData)
                    HDCameraData.clearColorMode = HDAdditionalCameraData.ClearColorMode.Sky;
            }
        }
    }
    private bool _depthEstimation = false;
    public bool DepthEstimation
    {
        get => _depthEstimation;
        set
        {
            if (_videoSeeThrough == value) return;
            _depthEstimation = value;

            if (_depthEstimation)
            {
                _depthEstimation = VarjoMixedReality.EnableDepthEstimation();
                if(!_depthEstimation)
                {
                    Debug.LogError("Error Enabling Depth Estimation");
                    return;
                }

                originalSubmitDepthValue = VarjoRendering.GetSubmitDepth();
                originalDepthSortingValue = VarjoRendering.GetDepthSorting();
                VarjoRendering.SetSubmitDepth(true);
                VarjoRendering.SetDepthSorting(true);
            }
            else
            {
                VarjoMixedReality.DisableDepthEstimation();
                VarjoRendering.SetSubmitDepth(originalSubmitDepthValue);
                VarjoRendering.SetDepthSorting(originalDepthSortingValue);
            }
        }
    }

    private float _currentVREyeOffset = 1.0f;
    [Range(0f, 1.0f)]
    public float VREyeOffset = 1.0f;

    [Header("Real Time Environment")]
    private bool _environmentReflections = false;
    public bool EnvironmentReflections
    {
        get => _environmentReflections;
        set
        {
            if (_environmentReflections != value)
            {
                _environmentReflections = value;
                if (_environmentReflections)
                {
                    if (VarjoMixedReality.environmentCubemapStream.IsSupported())
                    {
                        _environmentReflections = VarjoMixedReality.environmentCubemapStream.Start();
                        if (!_environmentReflections)
                        {
                            Debug.LogError("Error Starting Environment Cubemap Stream");
                            return;
                        }
                    }

                    if (!cameraSubsystem.IsMetadataStreamEnabled)
                        cameraSubsystem.EnableMetadataStream();
                    metadataStreamEnabled = cameraSubsystem.IsMetadataStreamEnabled;
                }
                else
                {
                    VarjoMixedReality.environmentCubemapStream.Stop();
                    cameraSubsystem.DisableMetadataStream();
                }
            }
        }
    }

    public int reflectionRefreshRate = 30;
    public VolumeProfile m_skyboxProfile = null;
    public Cubemap defaultSky = null;
    public CubemapEvent onCubemapUpdate = new();

    private bool metadataStreamEnabled = false;
    private VarjoCameraMetadataStream.VarjoCameraMetadataFrame metadataFrame;

    private VarjoEnvironmentCubemapStream.VarjoEnvironmentCubemapFrame cubemapFrame;

    private bool originalOpaqueValue = false;
    private bool originalSubmitDepthValue = false;
    private bool originalDepthSortingValue = false;

    private bool defaultSkyActive = false;
    private bool cubemapEventListenerSet = false;

    private HDRISky volumeSky = null;
    private Exposure volumeExposure = null;
    private VSTWhiteBalance volumeVSTWhiteBalance = null;

    private HDAdditionalCameraData HDCameraData;

    private VarjoCameraSubsystem cameraSubsystem;

    private void Start()
    {
        if (XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null)
        {
            var loader = XRGeneralSettings.Instance.Manager.ActiveLoaderAs<VarjoLoader>();
            if(loader == null)
            {
                Debug.LogWarning("No HMD connected. Using the non-activated loader");
                loader = XRGeneralSettings.Instance.Manager.activeLoaders[0] as VarjoLoader;
            }
            cameraSubsystem = loader.cameraSubsystem as VarjoCameraSubsystem;
        }

        if (cameraSubsystem != null)
            cameraSubsystem.Start();

        originalOpaqueValue = VarjoRendering.GetOpaque();
        VarjoRendering.SetOpaque(false);
        cubemapEventListenerSet = onCubemapUpdate.GetPersistentEventCount() > 0;
        HDCameraData = xrCamera.GetComponent<HDAdditionalCameraData>();

        if (!m_skyboxProfile.TryGet(out volumeSky))
            volumeSky = m_skyboxProfile.Add<HDRISky>(true);

        if (!m_skyboxProfile.TryGet(out volumeExposure))
            volumeExposure = m_skyboxProfile.Add<Exposure>(true);

        if (!m_skyboxProfile.TryGet(out volumeVSTWhiteBalance))
            volumeVSTWhiteBalance = m_skyboxProfile.Add<VSTWhiteBalance>(true);
    }

    void Update()
    {
        UpdateMRFeatures();
    }

    void UpdateMRFeatures()
    {
        UpdateVREyeOffSet();
        UpdateReflections();
    }

    void UpdateVREyeOffSet()
    {
        if (VREyeOffset == _currentVREyeOffset) return;

        VarjoMixedReality.SetVRViewOffset(VREyeOffset);
        _currentVREyeOffset = VREyeOffset;
    }

    void UpdateReflections()
    {
        if (EnvironmentReflections && metadataStreamEnabled)
        {
            if (VarjoMixedReality.environmentCubemapStream.hasNewFrame && cameraSubsystem.MetadataStream.hasNewFrame)
            {
                cubemapFrame = VarjoMixedReality.environmentCubemapStream.GetFrame();

                metadataFrame = cameraSubsystem.MetadataStream.GetFrame();
                float exposureValue = (float)metadataFrame.metadata.ev + Mathf.Log((float)metadataFrame.metadata.cameraCalibrationConstant, 2f);
                volumeExposure.fixedExposure.Override(exposureValue);

                volumeSky.hdriSky.Override(cubemapFrame.cubemap);
                volumeSky.updateMode.Override(EnvironmentUpdateMode.Realtime);
                volumeSky.updatePeriod.Override(1f / (float)reflectionRefreshRate);
                defaultSkyActive = false;

                volumeVSTWhiteBalance.intensity.Override(1f);

                // Set white balance normalization values
                Shader.SetGlobalColor("_CamWBGains", metadataFrame.metadata.wbNormalizationData.wbGains);
                Shader.SetGlobalMatrix("_CamInvCCM", metadataFrame.metadata.wbNormalizationData.invCCM);
                Shader.SetGlobalMatrix("_CamCCM", metadataFrame.metadata.wbNormalizationData.ccm);

                if (cubemapEventListenerSet)
                    onCubemapUpdate.Invoke();
            }
        }
        else if (!defaultSkyActive)
        {
            volumeSky.hdriSky.Override(defaultSky);
            volumeSky.updateMode.Override(EnvironmentUpdateMode.OnChanged);
            volumeExposure.fixedExposure.Override(6.5f);
            volumeVSTWhiteBalance.intensity.Override(0f);
            defaultSkyActive = true;
        }
    }

    void OnDisable()
    {
        VideoSeeThrough = false;
        DepthEstimation = false;
        EnvironmentReflections = false;
        UpdateMRFeatures();
        VarjoRendering.SetOpaque(originalOpaqueValue);
    }
}
