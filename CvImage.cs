//********************************************************************************************
//Author: Sergey Stoyan
//        sergey.stoyan@gmail.com
//        http://www.cliversoft.com
//********************************************************************************************
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Emgu.CV.Features2D;
using System.Collections.Generic;
using System.Linq;

namespace Cliver.PdfDocumentParser
{
    public class CvImage : IDisposable
    {
        public void Dispose()
        {
            lock (this)
            {
                if (image != null)
                {
                    image.Dispose();
                    image = null;
                }
                //if (Bitmap != null)
                //{
                //    Bitmap.Dispose();
                //    Bitmap = null;
                //}
            }
        }

        ~CvImage()
        {
            Dispose();
        }

        /// <summary>
        /// used only by serializer
        /// </summary>
        public string Data
        {
            get
            {
                byte[] hash = new byte[8 + image.Bytes.Length];
                Array.Copy(BitConverter.GetBytes(Width), 0, hash, 0, 4);
                Array.Copy(BitConverter.GetBytes(Height), 0, hash, 4, 4);

                Array.Copy(image.Bytes, 0, hash, 8, image.Bytes.Length);

                return Convert.ToBase64String(hash);
            }
            set
            {
                try
                {
                    byte[] hash = Convert.FromBase64String(value);
                    Width = BitConverter.ToInt32(hash, 0);
                    Height = BitConverter.ToInt32(hash, 4);

                    image = new Image<Gray, byte>(Width, Height);
                    byte[] bs = new byte[hash.Length - 8];
                    Array.Copy(hash, 8, bs, 0, bs.Length);
                    image.Bytes = bs;
                }
                catch
                {
                    Width = 10;
                    Height = 10;
                    image = new Image<Gray, byte>(Width, Height);
                }
            }
        }
        internal int Width { get; private set; }
        internal int Height { get; private set; }

        Image<Gray, byte> image;
        //internal Bitmap Bitmap
        //{
        //    set
        //    {
        //        _bitmap = value;
        //        Image<Gray, byte> Image = _bitmap.ToImage<Gray, byte>();
        //        Emgu.CV.CvInvoke.Blur(Image, Image, new Size(10, 10), new Point(0, 0));
        //        //Emgu.CV.CvInvoke.Threshold(image, image, 60, 255, ThresholdType.Otsu | ThresholdType.Binary);
        //        //Emgu.CV.CvInvoke.Erode(image, image, null, new Point(-1, -1), 1, BorderType.Constant, CvInvoke.MorphologyDefaultBorderValue);
        //        //CvInvoke.Dilate(image, image, null, new Point(-1, -1), 1, BorderType.Constant, CvInvoke.MorphologyDefaultBorderValue);
        //        //CvInvoke.Canny(image, image, 100, 30, 3);
        //    }
        //    get
        //    {
        //        return _bitmap;
        //    }
        //}
        //Bitmap _bitmap;

        public CvImage()// Used only by deserializer!!!
        { }

        public CvImage(Bitmap bitmap)
        {
            bitmap = Page.GetScaledImage2Pdf(bitmap);
            image = getPreprocessedImage(bitmap);
            Width = image.Width;
            Height = image.Height;
        }

        static private Image<Gray, byte> getPreprocessedImage(Bitmap bitmap)
        {
            Image<Gray, byte> image = bitmap.ToImage<Gray, byte>();
            //Emgu.CV.CvInvoke.Blur(image, image, new Size(10, 10), new Point(0, 0));
            //Emgu.CV.CvInvoke.Threshold(image, image, 60, 255, ThresholdType.Otsu | ThresholdType.Binary);
            //Emgu.CV.CvInvoke.Erode(image, image, null, new Point(-1, -1), 1, BorderType.Constant, CvInvoke.MorphologyDefaultBorderValue);
            //CvInvoke.Dilate(image, image, null, new Point(-1, -1), 1, BorderType.Constant, CvInvoke.MorphologyDefaultBorderValue);
            //CvInvoke.Canny(image, image, 100, 30, 3);
            return image;
        }

        public System.Drawing.Point? FindWithinImage(CvImage cvImage, float threshold, float scaleDeviation, int scaleStep/*, out float detectedScale*/)
        {
            System.Drawing.Point? p;
            p = findWithinImage(image, cvImage.image, threshold);
            if (p != null)
                return p;
            //running through pyramid
            int stepCount = Convert.ToInt32(scaleDeviation * Width / scaleStep);
            for (int i = 1; i <= stepCount; i++)
            {
                double scaleDelta = scaleStep * i / Width;
                Image<Gray, byte> template = image.Resize(1 + scaleDelta, Inter.Linear);
                p = findWithinImage(template, cvImage.image, threshold);
                if (p != null)
                    return p;
                template = image.Resize(1 - scaleDelta, Inter.Linear);
                p = findWithinImage(template, cvImage.image, threshold);
                if (p != null)
                    return p;
            }
            return null;
        }

        static System.Drawing.Point? findWithinImage(Image<Gray, byte> template, Image<Gray, byte> image, float threshold = 0.7f)
        {
            if (template.Width > image.Width || template.Height > image.Height)//otherwise MatchTemplate() throws an exception
                return null;
            using (Image<Gray, float> match = image.MatchTemplate(template, TemplateMatchingType.CcoeffNormed))
            {
                match.MinMax(out double[] min, out double[] max, out Point[] minPoint, out Point[] maxPoint);

                if (max[0] > threshold)
                    return maxPoint[0];
                return null;
            }
        }

        /// <summary>
        /// Run through pyramid and detect the best scale.
        /// </summary>
        /// <param name="cvImage"></param>
        /// <param name="padding"></param>
        /// <param name="threshold"></param>
        /// <param name="scaleDeviation"></param>
        /// <param name="scaleStep"></param>
        /// <param name="bestScale"></param>
        /// <returns></returns>
        public List<Match> FindWithinImage(CvImage cvImage, Size padding, float threshold, float scaleDeviation, int scaleStep, out float bestScale)
        {
            bestScale = 1;
            List<Match> bestMatches = findWithinImage(image, padding, cvImage.image, threshold);
            float bestScore = bestMatches.Max(a => a.Score);
            //running through pyramid
            int stepCount = Convert.ToInt32(scaleDeviation * Width / scaleStep);
            for (int i = 1; i <= stepCount; i++)
            {
                float scaleDelta = scaleStep * i / Width;
                float scale = 1 + scaleDelta;
                Image<Gray, byte> template = image.Resize(scale, Inter.Linear);
                List<Match> ms = findWithinImage(template, padding, cvImage.image, threshold);
                float s = ms.Max(a => a.Score);
                if (bestScore > s)
                    break;
                bestScale = scale;
                bestMatches = ms;
                bestScore = s;
            }
            for (int i = 1; i <= stepCount; i++)
            {
                float scaleDelta = scaleStep * i / Width;
                float scale = 1 - scaleDelta;
                Image<Gray, byte> template = image.Resize(scale, Inter.Linear);
                List<Match> ms = findWithinImage(template, padding, cvImage.image, threshold);
                float s = ms.Max(a => a.Score);
                if (bestScore > s)
                    break;
                bestScale = scale;
                bestMatches = ms;
                bestScore = s;
            }
            return bestMatches;
        }

        /// <summary>
        /// Runs through pyramid only until something is found. 
        /// </summary>
        /// <param name="cvImage"></param>
        /// <param name="padding"></param>
        /// <param name="threshold"></param>
        /// <param name="scaleDeviation"></param>
        /// <param name="scaleStep"></param>
        /// <returns></returns>
        public List<Match> FindWithinImage(CvImage cvImage, Size padding, float threshold, float scaleDeviation, int scaleStep)
        {
            List<Match> ms;
            ms = findWithinImage(image, padding, cvImage.image, threshold);
            if (ms.Count > 0)
                return ms;
            //running through pyramid
            int stepCount = Convert.ToInt32(scaleDeviation * Width / scaleStep);
            for (int i = 1; i <= stepCount; i++)
            {
                float scaleDelta = scaleStep * i / Width;
                Image<Gray, byte> template = image.Resize(1 + scaleDelta, Inter.Linear);
                ms = findWithinImage(template, padding, cvImage.image, threshold);
                if (ms.Count > 0)
                    return ms;
                template = image.Resize(1 - scaleDelta, Inter.Linear);
                ms = findWithinImage(template, padding, cvImage.image, threshold);
                if (ms.Count > 0)
                    return ms;
            }
            return ms;
        }

        public List<Match> FindWithinImage(CvImage cvImage, Size padding, float threshold, float scale)
        {
            Image<Gray, byte> template = image.Resize(scale, Inter.Linear);
            return findWithinImage(template, padding, cvImage.image, threshold);
        }

        static List<Match> findWithinImage(Image<Gray, byte> template, Size padding, Image<Gray, byte> image, float threshold = 0.7f)
        {
            if (template.Width > image.Width || template.Height > image.Height)//otherwise MatchTemplate() throws an exception
                return null;

            using (Image<Gray, float> match = image.MatchTemplate(template, TemplateMatchingType.CcoeffNormed))
            {
                float[,,] matches = match.Data;
                Dictionary<Rectangle, Match> paddedMatchRs2bestMatchP = new Dictionary<Rectangle, Match>();

                for (int x = matches.GetLength(1) - 1; x >= 0; x--)
                {
                    for (int y = matches.GetLength(0) - 1; y >= 0; y--)
                    {
                        float score = matches[y, x, 0];
                        //if (score < 0.003)//SqdiffNormed
                        if (score > threshold)//CcoeffNormed
                        {
                            Rectangle r = new Rectangle(new Point(x, y), template.Size);
                            var kv = paddedMatchRs2bestMatchP.FirstOrDefault(a => a.Key.Contains(r));
                            if (kv.Key == Rectangle.Empty)
                            {
                                Rectangle ar = new Rectangle(r.Location, r.Size);
                                ar.Inflate(padding);
                                paddedMatchRs2bestMatchP[ar] = new Match { Rectangle = r, Score = score };
                            }
                            else
                            {
                                if (kv.Value.Score < score)
                                    paddedMatchRs2bestMatchP[kv.Key] = new Match { Rectangle = r, Score = score };
                            }
                        }
                    }
                }
                return paddedMatchRs2bestMatchP.Values.ToList();
            }
        }

        public class Match
        {
            public Rectangle Rectangle;
            public float Score;
        }

        public Image GetImage()
        {
            return image.ToBitmap();
        }
    }
}