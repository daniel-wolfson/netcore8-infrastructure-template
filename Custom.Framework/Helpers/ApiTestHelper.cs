namespace Custom.Framework.Helpers
{
    public class ApiTestHelper
    {
        public static string GenerateIsraelID()
        {
            int[] id = new int[9];

            // Generate first 8 digits randomly
            for (int i = 0; i < 8; i++)
            {
                id[i] = Random.Shared.Next(0, 10);
            }

            // Calculate the check digit using Luhn algorithm
            int sum = 0;
            for (int i = 0; i < 8; i++)
            {
                int num = id[i] * ((i % 2) + 1); // Multiply by 1 or 2 alternatively
                sum += num > 9 ? num - 9 : num;  // Sum digits if num > 9
            }

            id[8] = (10 - sum % 10) % 10;
            return string.Join("", id);
        }

        public static string GenerateRandomString(int length, int requiredDigits = 0)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            const string digits = "0123456789";

            char[] result = new char[length];
            int digitsPlaced = 0;

            // Place required digits randomly
            while (digitsPlaced < requiredDigits)
            {
                int position = Random.Shared.Next(length);
                if (!char.IsDigit(result[position])) // Ensure we don't overwrite existing digits
                {
                    result[position] = digits[Random.Shared.Next(digits.Length)];
                    digitsPlaced++;
                }
            }

            // Fill the rest of the string with random characters
            for (int i = 0; i < length; i++)
            {
                if (result[i] == '\0') // Check if the position is empty
                {
                    result[i] = chars[Random.Shared.Next(chars.Length)];
                }
            }

            return new string(result);
        }
    }
}