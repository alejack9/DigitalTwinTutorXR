using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ToggleMR : MonoBehaviour
{
    public MRController mrController;

    [Header("MR feature toggle keys")]
    public KeyCode MRToggleKey = KeyCode.Alpha1;
    public KeyCode depthEstimationToggleKey = KeyCode.Alpha2;
    //public KeyCode reflectionToggleKey = KeyCode.Alpha3;
    public KeyCode VREyeOffsetToggleKey = KeyCode.Alpha3;
    public KeyCode DirectionalLightToggleKey = KeyCode.Alpha4;
    public GameObject directionalLight;

    // Start is called before the first frame update
    void Start()
    {
        if (!mrController)
            enabled = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.anyKeyDown)
        {
            if (Input.GetKeyDown(MRToggleKey))
                mrController.videoSeeThrough = !mrController.videoSeeThrough;
            if (Input.GetKeyDown(depthEstimationToggleKey))
                mrController.depthEstimation = !mrController.depthEstimation;
            //if (Input.GetKeyDown(reflectionToggleKey))
            //    mrController.environmentReflections = !mrController.environmentReflections;
            if (Input.GetKeyDown(VREyeOffsetToggleKey))
                if (mrController.VREyeOffset == 0f)
                    mrController.VREyeOffset = 1.0f;
                else
                    mrController.VREyeOffset = 0f;
            if (Input.GetKeyDown(DirectionalLightToggleKey))
                directionalLight.SetActive(!directionalLight.activeSelf);
        }
    }
}
