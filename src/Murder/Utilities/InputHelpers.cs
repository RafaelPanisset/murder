﻿namespace Murder.Utilities
{
    public static class InputHelpers
    {
        public static void Clamp(int length)
        {
            Game.Input.ClampText(length);
        }

        public static int GetAmountOfLines(ReadOnlySpan<char> text, int width)
        {
            float lineWidth = Game.Data.PixelFont.GetLineWidth(text);
            return Calculator.RoundToInt(lineWidth / width);
        }

        public static bool FitToWidth(ref ReadOnlySpan<char> text, int width)
        {
            bool changed = false;

            while (Game.Data.PixelFont.GetLineWidth(text) > width)
            {
                text = text[..^1];

                changed = true;
            }

            return changed;
        }
    }
}