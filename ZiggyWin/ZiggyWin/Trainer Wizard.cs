using System.Windows.Forms;

namespace ZeroWin
{
    public partial class Trainer_Wizard : Form
    {
        public Form1 ziggyWin;
        private TrainerValueInput inputDialog = new TrainerValueInput();

        public class Pokes
        {
            public byte bank;
            public int address;
            public int newVal;
            public int oldVal;
        }

        public class Trainer
        {
            public string name;
            public System.Collections.Generic.List<Pokes> pokeList = new System.Collections.Generic.List<Pokes>();
        }

        private System.Collections.Generic.List<Trainer> TrainerList = new System.Collections.Generic.List<Trainer>();

        public void LoadTrainer(string filename) {
            pokesListBox.Items.Clear();
            TrainerList.Clear();
            using (System.IO.FileStream fs = new System.IO.FileStream(filename, System.IO.FileMode.Open)) {
                System.IO.StreamReader sr = new System.IO.StreamReader(fs);
                string line;
                char[] delimiters = new char[] { '\r', '\n', ' ' };
                do {
                    line = sr.ReadLine();

                    if (line[0] == 'N') {
                        Trainer trainer = new Trainer();
                        trainer.name = line.Substring(1, line.Length - 1);
                        string[] fields;

                        do {
                            Pokes poke = new Pokes();
                            line = sr.ReadLine();
                            fields = line.Split(delimiters, System.StringSplitOptions.RemoveEmptyEntries);
                            poke.bank = System.Convert.ToByte(fields[1]);
                            poke.address = System.Convert.ToInt32(fields[2]);
                            poke.newVal = System.Convert.ToInt32(fields[3]);
                            poke.oldVal = System.Convert.ToInt32(fields[4]);
                            trainer.pokeList.Add(poke);
                        } while (fields[0] != "Z");

                        pokesListBox.Items.Add(trainer.name);
                        TrainerList.Add(trainer);
                    }
                } while (line[0] != 'Y');
            }
        }

        private void ApplyTrainers() {
            for (int f = 0; f < TrainerList.Count; f++) {
                Trainer trainer = TrainerList[f];
                bool applyPokes = (pokesListBox.GetItemCheckState(f) == CheckState.Checked);

                for (int g = 0; g < trainer.pokeList.Count; g++) {
                    Pokes p = trainer.pokeList[g];

                    if (applyPokes && (p.newVal > 255)) {
                        inputDialog.Title = trainer.name;
                        inputDialog.ShowDialog();
                        p.newVal = inputDialog.PokeValue;
                    }

                    if (p.bank == 8) //48k
                    {
                        //Remove poke only if old value is a non-zero value
                        if (!applyPokes && (p.oldVal == 0))
                            continue;

                        ziggyWin.zx.PokeByteNoContend(p.address, (applyPokes ? p.newVal : p.oldVal));
                    } else {
                        //Remove poke only if old value is a non-zero value
                        if (!applyPokes && (p.oldVal == 0))
                            continue;

                        // ziggyWin.zx.PokeBank(p.bank * 2 + (p.address >> 14), p.address % 16384, (applyPokes ? p.newVal : p.oldVal));
                        ziggyWin.zx.PokeByteNoContend(p.address, (applyPokes ? p.newVal : p.oldVal));
                    }
                }
            }
        }

        public Trainer_Wizard(Form1 zw) {
            InitializeComponent();
            // Set the default dialog font on each child control
            foreach (Control c in Controls) {
                c.Font = new System.Drawing.Font(System.Drawing.SystemFonts.MessageBoxFont.FontFamily, c.Font.Size);
            }
            ziggyWin = zw;
        }

        private void button1_Click(object sender, System.EventArgs e) {
            ApplyTrainers();
            this.Hide();
            MessageBox.Show("Selected pokes are now active.", "Pokes applied", MessageBoxButtons.OK, MessageBoxIcon.None);
        }

        private void button3_Click(object sender, System.EventArgs e) {
            openFileDialog1.Title = "Choose a .POK file";
            openFileDialog1.FileName = "";
            openFileDialog1.Filter = ".POK files|*.POK";

            if (openFileDialog1.ShowDialog() == DialogResult.OK) {
                label3.Text = openFileDialog1.SafeFileName;
                LoadTrainer(openFileDialog1.FileName);
            }
        }

        private void button2_Click(object sender, System.EventArgs e) {
            this.Hide();
        }

        private void pokesListBox_ItemCheck(object sender, ItemCheckEventArgs e) {
            if (e.NewValue != CheckState.Checked) {
                CheckedListBox.CheckedIndexCollection selectedItems = pokesListBox.CheckedIndices;
                if (selectedItems.Count == 1) {
                    button1.Enabled = false;
                    return;
                }
            }
            button1.Enabled = true;
        }
    }
}