using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DungeonCandleLoop : MonoBehaviour
{
    [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();
    [SerializeField] private SpriteRenderer[] candles = Array.Empty<SpriteRenderer>();
    [SerializeField, Min(1)] private int framesPerSecond = 30;

    private double startTime;
    private int currentFrame;

    private void OnEnable()
    {
        startTime = Time.timeAsDouble;
        currentFrame = -1;
    }

    private void Update()
    {
        if (frames.Length == 0)
            return;

        double elapsed = Time.timeAsDouble - startTime;
        int frame = (int)(elapsed * framesPerSecond % frames.Length);
        if (frame == currentFrame)
            return;

        currentFrame = frame;

        for (int i = 0; i < candles.Length; i++)
        {
            if (candles[i] == null)
                continue;

            int phaseOffset = i * frames.Length / candles.Length;
            candles[i].sprite = frames[(frame + phaseOffset) % frames.Length];
        }
    }
}
