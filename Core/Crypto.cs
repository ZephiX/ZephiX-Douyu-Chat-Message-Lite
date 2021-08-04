using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;


namespace Douyu
{
    class Crypto
    {
        const uint Delta = 0x9E3779B9;
        public static byte[] MD5Hash(byte[] data)
        {
            if (data != null)
            {
                using MD5 md = MD5.Create();
                byte[] b = md.ComputeHash(data);
                return b;
            }
            else
            {
                return new byte[16];
            }

        }

        public static uint[] XTEA(uint[] v, uint[] key, int round)
        {
            if (v.Length == 2 && key.Length == 4)
            {
                uint[] res = v;
                if (round > 0) enXTEA(ref res, key, Convert.ToUInt32(Math.Abs(round)));
                else deXTEA(ref res, key, Convert.ToUInt32(Math.Abs(round)));

                return res;
            }
            else return null;
        }

        public static uint[] TEA(uint[] v,uint[] key,bool encrytp)
        {
            if (v.Length == 2 && key.Length == 4)
            {
                uint[] res = v;
                if (encrytp) enTEA(ref res, key);
                else deTEA(ref res, key);

                return res;
            }
            else return null;
        }


        private static void deXTEA(ref uint[] v, uint[] key, uint round = 32)
        {

            uint v0, v1, sum;
            if (v.Length == 2 && key.Length == 4)
            {

                v0 = v[0]; v1 = v[1]; sum = Delta * round;
                for (int j = 0; j < round; j++)
                {
                    v1 -= (((v0 << 4) ^ (v0 >> 5)) + v0) ^ (sum + key[(sum >> 11) & 3]);
                    sum -= Delta;
                    v0 -= (((v1 << 4) ^ (v1 >> 5)) + v1) ^ (sum + key[sum & 3]);
                }
                v[0] = v0; v[1] = v1;


            }
        }

        private static void enXTEA(ref uint[] v, uint[] key, uint round)
        {
            uint v0, v1, sum;
            if (v.Length == 2 && key.Length == 4)
            {



                v0 = v[0]; v1 = v[1]; sum = 0;
                for (int j = 0; j < round; j++)
                {
                    v0 += (((v1 << 4) ^ (v1 >> 5)) + v1) ^ (sum + key[sum & 3]);
                    sum += Delta;
                    v1 += (((v0 << 4) ^ (v0 >> 5)) + v0) ^ (sum + key[(sum >> 11) & 3]);
                }
                v[0] = v0; v[1] = v1;
            }

        }

        private static void enTEA(ref uint[] v, uint[] key)
        {
            uint v0, v1, sum;
            if (v.Length == 2 && key.Length == 4)
            {

                v0 = v[0]; v1 = v[1]; sum = 0;
                for (int j = 0; j < 32; j++)
                {
                    sum += Delta;
                    v0 += ((v1 << 4) + key[0]) ^ (v1 + sum) ^ ((v1 >> 5) + key[1]);
                    v1 += ((v0 << 4) + key[2]) ^ (v0 + sum) ^ ((v0 >> 5) + key[3]);
                }
                v[0] = v0; v[1] = v1;

            }
        }

        private static void deTEA(ref uint[] v, uint[] key)
        {
            uint v0, v1, sum;
            if (v.Length == 2 && key.Length == 4)
            {

                v0 = v[0]; v1 = v[1]; sum = 0xC6EF3720;
                for (int j = 0; j < 32; j++)
                {
                    v1 -= ((v0 << 4) + key[2]) ^ (v0 + sum) ^ ((v0 >> 5) + key[3]);
                    v0 -= ((v1 << 4) + key[0]) ^ (v1 + sum) ^ ((v1 >> 5) + key[3]);
                    sum -= Delta;
                }
                v[0] = v0; v[1] = v1;

            }
        }


    }


}

