using UnityEngine;

public class Numbers
{
    public static int WrapAround(int num, int min, int max)
    {
        if (num < min)
        {
            num = max;
        }else if (num > max)
        {
            num = min;
        }
        return num;
    }

    public static int Sign(int num)
    {
        if (num > 0)
        {
            return 1;
        }else if (num < 0)
        {
            return -1;
        }
        else
        {
            return 0;
        }
    }
}
