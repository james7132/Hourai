using System;
using System.Security.Cryptography;

namespace Hourai {

  public static class RandomUtil {

    static RandomNumberGenerator _rng = RandomNumberGenerator.Create();

    public static int Int(int max) =>
      Int(0, max);

    public static int Int(int min, int max) {
      uint scale = uint.MaxValue;
      while (scale == uint.MaxValue) {
          // Get four random bytes.
          byte[] four_bytes = new byte[4];
          _rng.GetBytes(four_bytes);

          // Convert that into an uint.
          scale = BitConverter.ToUInt32(four_bytes, 0);
      }

      // Add min to the scaled difference between max and min.
      return (int)(min + (max - min) * (scale / (double)uint.MaxValue));
    }

  }

}
