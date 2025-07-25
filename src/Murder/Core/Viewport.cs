﻿using Murder.Core.Geometry;
using Murder.Core.Graphics;
using Murder.Utilities;
using System.Net.Http.Headers;
using System.Numerics;

namespace Murder.Core;

public readonly struct Viewport
{

    /// <summary>
    /// The size of the viewport (tipically the game's window)
    /// </summary>
    public readonly Point Size;
    /// <summary>
    /// The resolution that the game is actually rendered
    /// </summary>
    public readonly Point NativeResolution;

    /// <summary>
    /// The scale resuling in viewportSize/nativeResolution without any snapping.
    /// </summary>
    public readonly Vector2 OriginalScale;

    /// <summary>
    /// The scale that is applied to the native resolution before rendering
    /// </summary>
    public readonly Vector2 Scale;

    /// <summary>
    /// The rectangle where the game should be rendered on the screen.
    /// </summary>
    public readonly IntRectangle OutputRectangle;

    public readonly Vector2 Center;

    public readonly bool FailedConstraints;

    public Viewport(Point viewportSize, Point nativeResolution, ViewportResizeStyle resizeStyle)
    {
        Size = viewportSize;
        NativeResolution = nativeResolution;
        OriginalScale = viewportSize.ToVector2() / nativeResolution.ToVector2();

        switch (resizeStyle.ResizeMode)
        {
            case ViewportResizeMode.None:
                // Ignore the window size, use the game size from settings.
                Scale = Vector2.One;
                OutputRectangle = CenterOutput(nativeResolution.ToVector2(), viewportSize);
                break;

            case ViewportResizeMode.Stretch:
                // Stretch everything, ignoring aspect ratio.
                Scale = new Vector2(viewportSize.X / (float)nativeResolution.X, viewportSize.Y / (float)nativeResolution.Y);
                Scale = new(SnapToInt(Scale.X, resizeStyle.SnapToInteger, resizeStyle.RoundingMode), SnapToInt(Scale.Y, resizeStyle.SnapToInteger, resizeStyle.RoundingMode));

                OutputRectangle = new IntRectangle(0, 0, viewportSize.X, viewportSize.Y);
                break;

            case ViewportResizeMode.KeepRatio:
                {
                    //Scale the game to fit the window, keeping aspect ratio.
                    Vector2 stretchScale = new Vector2(viewportSize.X / (float)nativeResolution.X, viewportSize.Y / (float)nativeResolution.Y);
                    float minScale = Math.Min(stretchScale.X, stretchScale.Y);

                    Vector2 originalScale = new Vector2(minScale, minScale);
                    // Set the output rectangle to center the game in the window with the calculated scale to keep aspect ratio
                    OutputRectangle = CenterOutput(NativeResolution * originalScale, viewportSize);

                    Vector2 snappedScale = new(SnapToInt(originalScale.X, resizeStyle.SnapToInteger, resizeStyle.RoundingMode), SnapToInt(originalScale.Y, resizeStyle.SnapToInteger, resizeStyle.RoundingMode));
                    Scale = snappedScale;

                    // Now change trim the native resolution to account for the possible scale ceiling to integer
                    NativeResolution = new Point(Math.Min(nativeResolution.X, Calculator.RoundToInt(OutputRectangle.Width / snappedScale.X)),
                                           Math.Min(nativeResolution.Y, Calculator.RoundToInt(OutputRectangle.Height / snappedScale.Y)));
                }
                break;

            case ViewportResizeMode.AdaptiveLetterbox:
                {
                    // Letterbox the game, keeping aspect ratio with some allowance.

                    // Calculate the aspect ratios
                    float windowAspectRatio = viewportSize.X / (float)viewportSize.Y;
                    float unscaledAspectRatio = nativeResolution.X / (float)nativeResolution.Y;

                    // Interpolate between the unscaled and window aspect ratios based on the allowance
                    float targetAspectRatio;
                    if (unscaledAspectRatio < windowAspectRatio)
                    {
                        targetAspectRatio = Calculator.Approach(unscaledAspectRatio, windowAspectRatio, resizeStyle.PositiveApectRatioAllowance);
                    }
                    else
                    {
                        targetAspectRatio = Calculator.Approach(unscaledAspectRatio, windowAspectRatio, resizeStyle.NegativeApectRatioAllowance);
                    }

                    // Adjust the native resolution to match the target aspect ratio
                    Point adjustedNativeResolution = new Point(
                        Math.Min(nativeResolution.X, Calculator.RoundToInt(nativeResolution.Y * targetAspectRatio)),
                        Math.Min(nativeResolution.Y, Calculator.RoundToInt(nativeResolution.X / targetAspectRatio))
                        );

                    //Scale the game to fit the window, keeping aspect ratio.
                    Vector2 stretchScale = new Vector2(viewportSize.X / (float)adjustedNativeResolution.X, viewportSize.Y / (float)adjustedNativeResolution.Y);
                    float minScale = Math.Min(stretchScale.X, stretchScale.Y);
                    float snappedScale = SnapToInt(minScale, resizeStyle.SnapToInteger, resizeStyle.RoundingMode);
                    if (snappedScale - (Math.Truncate(snappedScale)) > 0.5f)
                    {
                        snappedScale = MathF.Ceiling(snappedScale);
                    }

                    float ceilingScale = Calculator.CeilToInt(minScale);

                    Vector2 originalScale = new Vector2(minScale, minScale);
                    // Set the output rectangle to center the game in the window with the calculated scale to keep aspect ratio
                    OutputRectangle = CenterOutput(adjustedNativeResolution * originalScale, viewportSize);
                    Scale = new Vector2(snappedScale);

                    // Now change trim the native resolution to account for the possible scale ceiling to integer
                    NativeResolution = new Point(
                        Calculator.CeilToInt(OutputRectangle.Width / snappedScale),
                        Calculator.CeilToInt(OutputRectangle.Height / snappedScale)
                        );
                }
                break;

            case ViewportResizeMode.Grow:
                {
                    Vector2 stretchedScale = new Vector2(viewportSize.X / (float)nativeResolution.X, viewportSize.Y / (float)nativeResolution.Y);
                    float ceiledYScale;
                    ceiledYScale = MathF.Ceiling(Math.Max(stretchedScale.Y - 0.1f, 1));

                    Vector2 outputSize = nativeResolution.ToVector2() * ceiledYScale;

                    // Now we see how many pixels are missing from the viewport and adjust the native resolution to fill the gap
                    Vector2 missingPixels = (viewportSize.ToVector2() - outputSize) / ceiledYScale;

                    NativeResolution = new Point(
                        Calculator.RoundToInt(nativeResolution.X + missingPixels.X),
                        Calculator.RoundToInt(nativeResolution.Y + missingPixels.Y)
                        );

                    OutputRectangle = CenterOutput(NativeResolution * ceiledYScale, viewportSize);
                    Scale = new Vector2(ceiledYScale);
                }
                break;
            case ViewportResizeMode.ConstrainedGrow:
                {
                    // Get min / max constraints, defaulting to sensible values if not provided
                    Point minRes = resizeStyle.MinNativeResolution ?? new Point(
                        Calculator.RoundToInt(nativeResolution.X * 0.8f),
                        Calculator.RoundToInt(nativeResolution.Y * 0.8f)
                    );
                    Point maxRes = resizeStyle.MaxNativeResolution ?? new Point(
                        Calculator.RoundToInt(nativeResolution.X * 1.2f),
                        Calculator.RoundToInt(nativeResolution.Y * 1.2f)
                    );
                    Vector2 stretchedScale = new Vector2(viewportSize.X / (float)nativeResolution.X, viewportSize.Y / (float)nativeResolution.Y);
                    int targetScale = (int)Math.Ceiling(Math.Max(stretchedScale.Y - 0.05f, 1));
                    Vector2 outputSize = nativeResolution.ToVector2() * targetScale;

                    // Now we see how many pixels are missing from the viewport and adjust the native resolution to fill the gap
                    Vector2 missingPixels = (viewportSize.ToVector2() - outputSize) / targetScale;

                    // Calculate the new native resolution based on the target scale
                    Point newNativeResolution = new Point(
                        Calculator.CeilingToInt(nativeResolution.X + missingPixels.X),
                        Calculator.CeilingToInt(nativeResolution.Y + missingPixels.Y)
                        );


                    if (newNativeResolution.X < minRes.X || newNativeResolution.Y < minRes.Y)
                    {
                        targetScale--;
                        if (targetScale < 1)
                        {
                            targetScale = 1;
                            FailedConstraints = true;
                        }

                        outputSize = nativeResolution.ToVector2() * targetScale;
                        missingPixels = (viewportSize.ToVector2() - outputSize) / targetScale;

                        // Calculate the new native resolution based on the target scale
                        newNativeResolution = new Point(
                            Calculator.CeilingToInt(nativeResolution.X + missingPixels.X),
                            Calculator.CeilingToInt(nativeResolution.Y + missingPixels.Y)
                            );
                    }


                    if (newNativeResolution.X > maxRes.X || newNativeResolution.Y > maxRes.Y)
                    {
                        newNativeResolution = new Point(
                            Math.Min(maxRes.X, newNativeResolution.X),
                            Math.Min(maxRes.Y, newNativeResolution.Y));
                    }

                    // Set the scale and output rectangle based on the new native resolution
                    Scale = new Vector2(targetScale);
                    OutputRectangle = CenterOutput(newNativeResolution.ToVector2() * (Scale), viewportSize);
                    NativeResolution = newNativeResolution;
                    break;
                }
            case ViewportResizeMode.Crop:
                // Center the game in the window, keeping everything else;
                Scale = Vector2.One;
                OutputRectangle = CenterOutput(viewportSize, nativeResolution);
                break;
            case ViewportResizeMode.AbsoluteScale:
                // Ignore the window size, use the game size from settings.
                Scale = Vector2.One * (resizeStyle.AbsoluteScale ?? 1);
                NativeResolution = (viewportSize / Scale).Point();
                OutputRectangle = new IntRectangle(0, 0, viewportSize.X, viewportSize.Y);
                break;
            default:
                throw new Exception($"Invalid window resize mode ({resizeStyle.ResizeMode}).");
        }

        // Cache
        Center = NativeResolution / 2f;
    }

    private static float SnapToInt(float scale, float snapToIntegerThreshold, RoundingMode roundingMode)
    {
        float remaining = scale - MathF.Round(scale);

        if (remaining > 0 && remaining < snapToIntegerThreshold)
        {
            switch (roundingMode)
            {
                case RoundingMode.Round:
                    return MathF.Round(scale);
                case RoundingMode.Floor:
                    return MathF.Floor(scale);
                case RoundingMode.Ceiling:
                    return scale;
                case RoundingMode.None:
                    return scale;
                default:
                    throw new Exception("Unknown rounding mode");
            }
        }
        else if (remaining < 0 && remaining > -snapToIntegerThreshold)
        {
            switch (roundingMode)
            {
                case RoundingMode.Round:
                    return MathF.Round(scale);
                case RoundingMode.Floor:
                    return scale;
                case RoundingMode.Ceiling:
                    return MathF.Ceiling(scale);
                case RoundingMode.None:
                    return scale;
                default:
                    throw new Exception("Unknown rounding mode");
            }
        }

        return scale;
    }

    private static IntRectangle CenterOutput(Vector2 targetSize, Vector2 viewportSize)
    {
        return new IntRectangle(
            Calculator.RoundToInt((viewportSize.X - targetSize.X) / 2f),
            Calculator.RoundToInt((viewportSize.Y - targetSize.Y) / 2f),
            targetSize.X,
            targetSize.Y);
    }
    public bool HasChanges(Point size, Vector2 scale)
    {
        return Size != size || Scale != scale;
    }
}
