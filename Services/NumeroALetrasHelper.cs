using System;

namespace SistemaChotaExpress.Services
{
    public static class NumeroALetrasHelper
    {
        public static string Convertir(decimal total, bool conPrefijoSon = true)
        {
            long entero = (long)Math.Truncate(total);
            int decimales = (int)Math.Round((total - entero) * 100);

            string textoEntero = ConvertirEntero(entero).Trim();
            if (string.IsNullOrWhiteSpace(textoEntero))
            {
                textoEntero = "cero";
            }

            string prefijo = conPrefijoSon ? "SON: " : "";
            return $"{prefijo}{textoEntero.ToUpperInvariant()} CON {decimales:D2}/100 SOLES";
        }

        private static string ConvertirEntero(long n)
        {
            if (n == 0) return "cero";
            if (n < 0) return "menos " + ConvertirEntero(Math.Abs(n));

            if (n == 100) return "cien";
            if (n < 100)
            {
                return ConvertirMenorQueCien((int)n);
            }
            if (n < 1000)
            {
                int centenas = (int)(n / 100);
                long resto = n % 100;
                string[] cText = { "", "ciento", "doscientos", "trescientos", "cuatrocientos", "quinientos", "seiscientos", "setecientos", "ochocientos", "novecientos" };
                return cText[centenas] + (resto > 0 ? " " + ConvertirEntero(resto) : "");
            }
            if (n < 1000000)
            {
                long miles = n / 1000;
                long resto = n % 1000;
                string prefijo = miles == 1 ? "mil" : ConvertirEntero(miles) + " mil";
                return prefijo + (resto > 0 ? " " + ConvertirEntero(resto) : "");
            }
            if (n < 1000000000)
            {
                long millones = n / 1000000;
                long resto = n % 1000000;
                string prefijo = millones == 1 ? "un millón" : ConvertirEntero(millones) + " millones";
                return prefijo + (resto > 0 ? " " + ConvertirEntero(resto) : "");
            }

            return n.ToString();
        }

        private static string ConvertirMenorQueCien(int n)
        {
            string[] unidades = { "cero", "un", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve", "diez",
                                  "once", "doce", "trece", "catorce", "quince", "dieciséis", "diecisiete", "dieciocho", "diecinueve" };
            if (n < 20) return unidades[n];

            if (n == 20) return "veinte";
            if (n < 30)
            {
                string[] veintis = { "veinte", "veintiuno", "veintidós", "veintitrés", "veinticuatro", "veinticinco", "veintiséis", "veintisiete", "veintiocho", "veintinueve" };
                return veintis[n - 20];
            }

            string[] decenas = { "", "", "", "treinta", "cuarenta", "cincuenta", "sesenta", "setenta", "ochenta", "noventa" };
            int d = n / 10;
            int u = n % 10;
            return decenas[d] + (u > 0 ? " y " + (u == 1 ? "un" : unidades[u]) : "");
        }
    }
}
