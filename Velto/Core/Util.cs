namespace Velto.Core;

public class Util
{
    public static float MapRange(float input, float input_L, float input_H, float out_L, float out_H) => out_L+((input-input_L)/(input_H-input_L))*(out_H-out_L);
}