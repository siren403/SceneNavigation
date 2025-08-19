using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using UnityEngine;

namespace GettingStarted
{
    [RequireComponent(typeof(CanvasGroup))]
    public class FadeCanvas : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;

        [SerializeField, Range(0, 1)] private float amount = 1f;

        public float Amount
        {
            get => amount;
            set
            {
                amount = Mathf.Clamp01(value);
                canvasGroup.alpha = amount;
                canvasGroup.interactable = amount > 0;
                canvasGroup.blocksRaycasts = amount > 0;
            }
        }

        private void OnValidate()
        {
            Amount = amount;
        }

        private void Reset()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }


    public static class FadeCanvasExtensions
    {
        public static UniTask InAsync(this FadeCanvas fade, float duration = 1f, CancellationToken ct = default)
        {
            return LMotion.Create(0f, 1f, duration)
                .Bind(fade, static (value, state) => state.Amount = value)
                .ToUniTask(ct);
        }

        public static UniTask OutAsync(this FadeCanvas fade, float duration = 1f, CancellationToken ct = default)
        {
            return LMotion.Create(1f, 0f, duration)
                .Bind(fade, static (value, state) => state.Amount = value)
                .ToUniTask(ct);
        }
    }
}