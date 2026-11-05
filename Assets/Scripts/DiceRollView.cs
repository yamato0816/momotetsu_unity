using System;
using System.Collections;
using UnityEngine;

namespace RailTrip3D
{
    /// <summary>
    /// Six-sided 3D die made from geometry and pips; deterministic final face from
    /// the game's authoritative outcome (never decide game result from animation).
    /// </summary>
    public sealed class DiceRollView : MonoBehaviour
    {
        private Transform die;
        private GameObject visual;
        private Coroutine active;
        public bool IsRolling { get; private set; }
        public int LastFace { get; private set; }

        private static readonly Color Ivory = new Color(.98f,.97f,.88f);
        private static readonly Color Ink = new Color(.16f,.24f,.30f);
        private static readonly Vector3[] Normals = {
            Vector3.up, Vector3.forward, Vector3.right, Vector3.left, Vector3.back, Vector3.down
        };
        private static readonly int[][] Dots = {
            new[] { 4 },
            new[] { 0, 8 },
            new[] { 0, 4, 8 },
            new[] { 0, 2, 6, 8 },
            new[] { 0, 2, 4, 6, 8 },
            new[] { 0, 2, 3, 5, 6, 8 }
        };
        private GameObject Part(PrimitiveType type, string label, Vector3 localPosition, Vector3 size,
            Color color, Quaternion rotation)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = label;
            obj.transform.SetParent(die, false);
            obj.transform.localPosition = localPosition;
            obj.transform.localRotation = rotation;
            obj.transform.localScale = size;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            obj.GetComponent<Renderer>().material = new Material(shader) { color = color };
            var collider = obj.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            return obj;
        }
        private void EnsureVisual()
        {
            if (visual != null) return;
            visual = new GameObject("Animated physical six-sided die");
            die = visual.transform;
            Part(PrimitiveType.Cube, "Rounded ivory core", Vector3.zero,
                Vector3.one * 1.23f, Ivory, Quaternion.identity);
            for (int face=0;face<6;face++)
            {
                Vector3 normal = Normals[face];
                Quaternion orientation = Quaternion.FromToRotation(Vector3.forward, normal);
                Part(PrimitiveType.Cube, "Face " + (face+1), normal*.627f,
                    new Vector3(1.08f,1.08f,.028f),Ivory,orientation);
                foreach (int grid in Dots[face])
                {
                    int column = grid%3-1, row = 1-grid/3;
                    Vector3 offset = orientation * new Vector3(column*.33f,row*.33f,0);
                    Part(PrimitiveType.Sphere, "Pip", normal*.654f + offset,
                        new Vector3(.16f,.16f,.065f),Ink,orientation);
                }
            }
            visual.SetActive(false);
        }
        private static Quaternion FaceUp(int value)
        {
            switch (value)
            {
                case 1: return Quaternion.identity;
                case 2: return Quaternion.Euler(-90,0,0);
                case 3: return Quaternion.Euler(0,0,-90);
                case 4: return Quaternion.Euler(0,0,90);
                case 5: return Quaternion.Euler(90,0,0);
                default: return Quaternion.Euler(0,0,180);
            }
        }
        public void Cancel()
        {
            if (active != null) StopCoroutine(active);
            active = null;
            IsRolling = false;
            if (visual != null) visual.SetActive(false);
        }
        public void Play(int face, Vector3 stationPosition)
        {
            Cancel();
            EnsureVisual();
            LastFace = Mathf.Clamp(face, 1, 6);
            active = StartCoroutine(Animate(stationPosition, LastFace));
        }
        private IEnumerator Animate(Vector3 stationPosition, int face)
        {
            IsRolling = true;
            visual.SetActive(true);
            // Dice floats up above the train, bounces twice, then resolves visibly.
            Vector3 basePosition = stationPosition + new Vector3(0,3.5f,0);
            die.position = basePosition;
            die.localScale = Vector3.one * 1.28f;
            float t = 0;
            const float spinTime = .94f;
            Quaternion startRotation = Quaternion.identity;
            while (t < spinTime)
            {
                t += Time.deltaTime;
                float fraction = Mathf.Clamp01(t/spinTime);
                float hop = Mathf.Abs(Mathf.Sin(fraction*Mathf.PI*2.8f)) *
                    (1.0f-.48f*fraction)*1.7f;
                die.position = basePosition + Vector3.up*hop;
                die.rotation = Quaternion.Euler(940*t,670*t,760*t);
                startRotation = die.rotation;
                yield return null;
            }
            Quaternion settled = FaceUp(face);
            // Eased landing with a small final bounce. Top face matches the game roll.
            t = 0;
            const float settleTime = .42f;
            while (t < settleTime)
            {
                t += Time.deltaTime;
                float progress = Mathf.Clamp01(t/settleTime);
                float smooth = 1f-Mathf.Pow(1f-progress,3);
                die.rotation = Quaternion.Slerp(startRotation,settled,smooth);
                die.position = basePosition+Vector3.up*(Mathf.Sin(progress*Mathf.PI)*.16f);
                yield return null;
            }
            die.rotation = settled;
            die.position = basePosition;
            yield return new WaitForSeconds(.32f);
            // Never hold up input once the roll has finished.
            visual.SetActive(false);
            IsRolling = false;
            active = null;
        }
    }
}
