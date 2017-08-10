using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace AigisPackager
{
    public partial class Form1 : Form
    {
        AL nowAL;
        string filePath;
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            /*FontDialog fd = new FontDialog();
            if(fd.ShowDialog() == DialogResult.OK)
            {
                Console.WriteLine(fd.Font.Name);
            }*/
        }

        public void Parse(string path)
        {
            FileStream fs = new FileStream(path, FileMode.Open);
            byte[] bodyBuffer = new byte[fs.Length];
            fs.Read(bodyBuffer, 0, bodyBuffer.Length);
            fs.Close();
            nowAL = ParseObject(bodyBuffer);
        }

        public AL ParseObject(byte[] buffer)
        {
            //到时候这个也要拿走的
            string type = Encoding.ASCII.GetString(buffer.Take(4).ToArray());
            byte[] body = buffer;
            AL al = new AL(buffer);
            Console.WriteLine(type);
            switch (type)
            {
                case "ALLZ":
                    byte[] decompressedBuffer = ALLZ.Decompress(body);
                    AL result = ParseObject(decompressedBuffer);
                    if(result.GetType().Name != "AL") al = result;
                    break;
                case "ALTB":
                    al = new ALTB(body);
                    break;
                case "ALAR":
                    al = new ALAR(body);
                    ALAR alar = (ALAR)al;
                    for(int i = 0; i < alar.Count; i++)
                    {
                        alar.Files[i].ParsedContent = ParseObject(alar.Files[i].Content);
                    }
                    break;
                case "ALFT":
                    al = new ALFT(body);
                    break;
            }
            return al;
        }

        private void outputFile(AL al,string path,string filename)
        {
            string type = al.GetType().Name;
            string filepath = "";
            switch (type)
            {
                case "ALAR":
                    ALAR alar = (ALAR)al;
                    string folderPath = Path.Combine(
                                            path,
                                            Path.GetFileNameWithoutExtension(filename)
                                        );
                    if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                    for(int i = 0; i < alar.Count; i++)
                    {
                        outputFile(alar.Files[i].ParsedContent, folderPath, alar.Files[i].Name);
                    }
                    break;
                case "ALTB":
                    
                    filePath = Path.Combine(
                                            path,
                                            Path.GetFileNameWithoutExtension(filename) + ".txt"
                                      );
                    ALTB altb = (ALTB)al;
                    if (altb.stringDictionary == null) return;
                    string[] Field = altb.GetStringFields();
                    StreamWriter sw = new StreamWriter(filePath);
                    foreach (string s in Field)
                    {
                        sw.WriteLine(s);
                    }
                    sw.Close();
                    break;
                case "ALFT":
                    filepath = Path.Combine(path,
                                            Path.GetFileNameWithoutExtension(filename) + ".png");
                    ALFT alft = (ALFT)al;
                    Bitmap image = alft.FontImage.Image;
                    image.Save(filepath,System.Drawing.Imaging.ImageFormat.Png);
                    break;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            outputFile(nowAL, Path.GetDirectoryName(filePath), Path.GetFileName(filePath));
        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.All;
        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            string[] s = (string[])e.Data.GetData(DataFormats.FileDrop, false);
            string filename = s[0];
            filePath = filename;
            Parse(filePath);
            button1.Enabled = true;
            button2.Enabled = true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            nowAL.SaveFile(filePath);
        }
    }
}
