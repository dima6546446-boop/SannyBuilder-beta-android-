using UnityEngine;
using RussianDrift.Core;

namespace RussianDrift.Vehicle
{
    /// <summary>Head/tail/brake/reverse/indicator lights + night beams. Materials are unlit so bloom picks up HDR colours.</summary>
    public class CarLights : MonoBehaviour
    {
        public Material headMat, tailMat, indLeftMat, indRightMat, reverseMat, neonMat;
        public Light[] headLights;
        public Renderer[] beamRenderers;
        public Material beamMat;
        public Light neonLight;

        public bool headlightsForced;
        public int indicatorMode;            // 0 off, -1 left, 1 right, 2 hazard
        public bool neonOn = true;

        private VehicleController vc;
        private float blink;
        private Color headOff = new Color(0.55f, 0.55f, 0.5f), headOn = new Color(3.2f, 3.0f, 2.4f);
        private Color tailOff = new Color(0.35f, 0.03f, 0.03f), tailOn = new Color(1.6f, 0.08f, 0.05f), tailBrake = new Color(4.2f, 0.15f, 0.08f);
        private Color indOff = new Color(0.35f, 0.18f, 0.02f), indOn = new Color(4f, 1.9f, 0.1f);
        private Color revOff = new Color(0.45f, 0.45f, 0.45f), revOn = new Color(3f, 3f, 3f);
        private Color neonColor = Color.clear;

        public void Bind(VehicleController v) { vc = v; }

        public void SetNeon(Color c)
        {
            neonColor = c;
            if (neonLight != null) { neonLight.color = c; neonLight.enabled = c.a > 0.01f; }
        }

        private void Update()
        {
            bool night = WorldConditions.NightFactor > 0.28f || WorldConditions.Rain > 0.45f || WorldConditions.Fog > 0.5f;
            bool head = headlightsForced || night;
            bool brake = vc != null && vc.BrakeLightOn;
            bool rev = vc != null && vc.ReverseOn && vc.SpeedMs > 0.05f;

            if (headMat != null) SetEmission(headMat, head ? headOn : headOff);
            if (tailMat != null) SetEmission(tailMat, brake ? tailBrake : (head ? tailOn : tailOff));
            if (reverseMat != null) SetEmission(reverseMat, rev ? revOn : revOff);

            int mode = indicatorMode;
            if (vc != null && mode == 0 && vc.SpeedMs < 12f && vc.SpeedMs > 1f)
            {
                if (vc.SteerInputSmoothed > 0.55f) mode = 1; else if (vc.SteerInputSmoothed < -0.55f) mode = -1;
            }
            blink += Time.deltaTime * 3.2f;
            bool phase = Mathf.Repeat(blink, 2f) < 1f;
            bool left = phase && (mode == -1 || mode == 2), right = phase && (mode == 1 || mode == 2);
            if (indLeftMat != null) SetEmission(indLeftMat, left ? indOn : indOff);
            if (indRightMat != null) SetEmission(indRightMat, right ? indOn : indOff);

            if (headLights != null)
                for (int i = 0; i < headLights.Length; i++)
                    if (headLights[i] != null) headLights[i].enabled = head && QualityManager.Current.additionalLights >= 3;
            if (beamMat != null)
            {
                float a = head ? Mathf.Clamp01(WorldConditions.NightFactor * 1.4f + WorldConditions.Fog * 0.8f + WorldConditions.Rain * 0.4f) : 0f;
                Color c = new Color(1f, 0.95f, 0.8f, 1f) * (a * 0.55f);
                if (beamMat.HasProperty("_TintColor")) beamMat.SetColor("_TintColor", c);
                MatLib.SetColor(beamMat, c);
                if (beamRenderers != null) for (int i = 0; i < beamRenderers.Length; i++) if (beamRenderers[i] != null) beamRenderers[i].enabled = a > 0.02f;
            }
            if (neonMat != null)
            {
                Color nc = neonOn && neonColor.a > 0.01f ? new Color(neonColor.r * 3f, neonColor.g * 3f, neonColor.b * 3f, 1f) : Color.black;
                SetEmission(neonMat, nc);
            }
        }

        private static void SetEmission(Material m, Color c)
        {
            MatLib.SetColor(m, c);
        }
    }
}
