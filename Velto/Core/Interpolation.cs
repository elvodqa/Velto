namespace Velto.Core;

public static class Interpolation
{
    public static float Map(float input, float input_L, float input_H, float out_L, float out_H) =>
        out_L + ((input - input_L) / (input_H - input_L)) * (out_H - out_L);
}