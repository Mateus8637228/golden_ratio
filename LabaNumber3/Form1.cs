using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using org.mariuszgromada.math.mxparser;

namespace LabaNumber3
{
  public partial class Form1 : Form
  {
    // Элементы UI
    private TextBox txtA, txtB, txtE, txtFormula;
    private Label lblResult;
    private Chart chart1;
    private MenuStrip menuStrip1;
    private ToolStripMenuItem calcMenuItem, plotMenuItem, clearMenuItem, stepMenuItem, pauseMenuItem;
    private ToolStripComboBox methodComboBox;

    // Для анимации
    private Timer animTimer;
    private bool isAnimating = false;
    private bool isPaused = false;

    // Состояние для Дихотомии
    private double dichA, dichB, dichMid, dichFmid;
    private int dichIter = 0;

    // Состояние для Золотого сечения
    private double goldA, goldB, goldX1, goldX2, goldF1, goldF2;
    private int goldIter = 0;
    private bool goldIsMax;

    // Константы золотого сечения
    private const double Phi = 1.6180339887498948482;
    private const double Resphi = 0.3819660112501051518;

    public Form1()
    {
      this.Text = "Лаба №4: Численные методы (Интерактив)";
      this.Size = new Size(1250, 750);
      this.StartPosition = FormStartPosition.CenterScreen;

      License.iConfirmNonCommercialUse("Student Lab");

      InitializeCustomComponents();
    }

    private void InitializeCustomComponents()
    {
      // === 1. Меню ===
      menuStrip1 = new MenuStrip();

      ToolStripLabel methodLabel = new ToolStripLabel("Метод:");
      methodComboBox = new ToolStripComboBox();
      methodComboBox.Items.AddRange(new string[] {
                "Дихотомия (Корень)",
                "Золотое сечение (Экстремумы)"
            });
      methodComboBox.SelectedIndex = 1;

      calcMenuItem = new ToolStripMenuItem("Рассчитать (Сразу)");
      stepMenuItem = new ToolStripMenuItem("Пошагово (Старт)");
      pauseMenuItem = new ToolStripMenuItem("Пауза / Продолжить");
      plotMenuItem = new ToolStripMenuItem("Построить график");
      clearMenuItem = new ToolStripMenuItem("Очистить");

      calcMenuItem.Click += CalcMenuItem_Click;
      stepMenuItem.Click += StepMenuItem_Click;
      pauseMenuItem.Click += PauseMenuItem_Click;
      plotMenuItem.Click += PlotMenuItem_Click;
      clearMenuItem.Click += ClearMenuItem_Click;

      pauseMenuItem.Enabled = false;

      menuStrip1.Items.Add(methodLabel);
      menuStrip1.Items.Add(methodComboBox);
      menuStrip1.Items.Add(new ToolStripSeparator());
      menuStrip1.Items.Add(calcMenuItem);
      menuStrip1.Items.Add(stepMenuItem);
      menuStrip1.Items.Add(pauseMenuItem);
      menuStrip1.Items.Add(plotMenuItem);
      menuStrip1.Items.Add(clearMenuItem);

      this.Controls.Add(menuStrip1);
      this.MainMenuStrip = menuStrip1;

      // === 2. Панель ввода ===
      Panel leftPanel = new Panel();
      leftPanel.Dock = DockStyle.Left;
      leftPanel.Width = 380;
      leftPanel.Padding = new Padding(10);
      leftPanel.Top = menuStrip1.Bottom;
      leftPanel.BackColor = Color.WhiteSmoke;
      leftPanel.AutoScroll = true; // Включаем прокрутку для панели
      this.Controls.Add(leftPanel);

      CreateInputPair(leftPanel, "a (начало):", 30, out txtA);
      CreateInputPair(leftPanel, "b (конец):", 80, out txtB);
      CreateInputPair(leftPanel, "e (погрешность):", 130, out txtE);
      CreateInputPair(leftPanel, "f(x):", 180, out txtFormula);

      txtA.Text = "-5";
      txtB.Text = "5";
      txtE.Text = "0,1";
      txtFormula.Text = "x^3 - 3*x + 1";

      lblResult = new Label();
      lblResult.Location = new Point(10, 240);
      lblResult.Size = new Size(340, 450);
      lblResult.Text = "Выберите метод и нажмите 'Пошагово'...";
      lblResult.Font = new Font("Consolas", 9, FontStyle.Regular);
      lblResult.BackColor = Color.White;
      lblResult.BorderStyle = BorderStyle.FixedSingle;
      lblResult.Padding = new Padding(5);
      leftPanel.Controls.Add(lblResult);

      // === 3. График ===
      chart1 = new Chart();
      chart1.Dock = DockStyle.Fill;

      ChartArea chartArea = new ChartArea("MainArea");
      chartArea.AxisX.MajorGrid.LineColor = Color.LightGray;
      chartArea.AxisY.MajorGrid.LineColor = Color.LightGray;
      chart1.ChartAreas.Add(chartArea);

      Legend legend = new Legend("MainLegend");
      legend.Docking = Docking.Top;
      chart1.Legends.Add(legend);

      this.Controls.Add(chart1);
      chart1.BringToFront();

      SetupChartSeries();

      // === 4. Таймер ===
      animTimer = new Timer();
      animTimer.Interval = 1000;
      animTimer.Tick += AnimTimer_Tick;
    }

    private void CreateInputPair(Panel parent, string labelText, int yPos, out TextBox textBox)
    {
      Label lbl = new Label();
      lbl.Text = labelText;
      lbl.Location = new Point(10, yPos);
      lbl.AutoSize = true;
      parent.Controls.Add(lbl);

      textBox = new TextBox();
      textBox.Location = new Point(180, yPos - 3);
      textBox.Width = 150;
      textBox.KeyPress += TextBox_KeyPress;
      parent.Controls.Add(textBox);
    }

    private void TextBox_KeyPress(object sender, KeyPressEventArgs e)
    {
      TextBox tb = (TextBox)sender;
      if (char.IsControl(e.KeyChar)) return;
      if (e.KeyChar == ' ') { e.Handled = true; return; }
      if (char.IsDigit(e.KeyChar))
      {
        if (e.KeyChar == '0' && tb.SelectionStart == 0 && tb.Text.Length > 0)
        {
          if (tb.Text[0] != ',' && tb.Text[0] != '.')
          {
            if (tb.Text.Length > 1 && char.IsDigit(tb.Text[1])) { e.Handled = true; return; }
          }
        }
        return;
      }
      if (e.KeyChar == ',') { if (tb.Text.Contains(",")) e.Handled = true; return; }
      if (e.KeyChar == '-') { if (tb.SelectionStart != 0 || tb.Text.Contains("-")) e.Handled = true; return; }
      e.Handled = true;
    }

    // ================= УПРАВЛЕНИЕ АНИМАЦИЕЙ =================

    private void StepMenuItem_Click(object sender, EventArgs e)
    {
      if (!ValidateInputs(out double a, out double b, out double eps, out string formula)) return;

      animTimer.Stop();
      isAnimating = false;
      isPaused = false;
      pauseMenuItem.Enabled = true;
      pauseMenuItem.Text = "Пауза";

      chart1.Series["Корень"].Points.Clear();
      chart1.Series["Экстремум"].Points.Clear();
      chart1.Series["Интервал"].Points.Clear();
      DrawGraph(formula, a, b);

      string method = methodComboBox.SelectedItem.ToString();

      if (method.Contains("Дихотомия"))
      {
        dichA = a; dichB = b; dichIter = 0;
        lblResult.Text = "=== ДИХОТОМИЯ: СТАРТ ===\n";
      }
      else
      {
        goldA = a; goldB = b; goldIter = 0;
        goldX1 = goldA + Resphi * (goldB - goldA);
        goldX2 = goldB - Resphi * (goldB - goldA);
        goldF1 = EvaluateFunction(formula, goldX1);
        goldF2 = EvaluateFunction(formula, goldX2);

        goldIsMax = EvaluateFunction(formula, a) > EvaluateFunction(formula, b);

        lblResult.Text = $"=== ЗОЛОТОЕ СЕЧЕНИЕ ({(goldIsMax ? "МАКСИМУМ" : "МИНИМУМ")}) ===\n";
      }

      isAnimating = true;
      animTimer.Start();
    }

    private void PauseMenuItem_Click(object sender, EventArgs e)
    {
      if (!isAnimating) return;

      if (isPaused)
      {
        isPaused = false;
        animTimer.Start();
        pauseMenuItem.Text = "Пауза";
        lblResult.Text += "\n[ПРОДОЛЖЕНИЕ]";
      }
      else
      {
        isPaused = true;
        animTimer.Stop();
        pauseMenuItem.Text = "Продолжить";
        lblResult.Text += "\n[ПАУЗА]";
      }
    }

    private void AnimTimer_Tick(object sender, EventArgs e)
    {
      if (!isAnimating || isPaused) return;

      string method = methodComboBox.SelectedItem.ToString();
      string formula = txtFormula.Text;
      double eps = double.Parse(txtE.Text.Replace('.', ','));

      if (method.Contains("Дихотомия"))
      {
        if (Math.Abs(dichB - dichA) <= eps || dichIter > 200)
        {
          FinishAnimation($"Корень найден: x ≈ {Math.Round(dichMid, 4)}\nf(x) = {Math.Round(dichFmid, 4)}");
          chart1.Series["Корень"].Points.AddXY(dichMid, dichFmid);
          return;
        }

        dichIter++;
        dichMid = (dichA + dichB) / 2.0;
        dichFmid = EvaluateFunction(formula, dichMid);

        string log = $"\n[Итерация {dichIter}]\n" +
                     $"a = {Math.Round(dichA, 4)}, b = {Math.Round(dichB, 4)}\n" +
                     $"x_mid = {Math.Round(dichMid, 4)}\n" +
                     $"f(x_mid) = {Math.Round(dichFmid, 4)}\n";
        lblResult.Text += log;

        chart1.Series["Интервал"].Points.Clear();
        chart1.Series["Интервал"].Points.AddXY(dichA, 0);
        chart1.Series["Интервал"].Points.AddXY(dichB, 0);
        chart1.Series["Корень"].Points.Clear();
        chart1.Series["Корень"].Points.AddXY(dichMid, dichFmid);

        if (EvaluateFunction(formula, dichA) * dichFmid < 0) dichB = dichMid;
        else dichA = dichMid;
      }
      else // Золотое сечение
      {
        if (Math.Abs(goldB - goldA) <= eps || goldIter > 200)
        {
          double resX = (goldA + goldB) / 2;
          double resY = EvaluateFunction(formula, resX);
          FinishAnimation($"Экстремум найден: x ≈ {Math.Round(resX, 4)}\nf(x) = {Math.Round(resY, 4)}");
          chart1.Series["Экстремум"].Points.AddXY(resX, resY);
          return;
        }

        goldIter++;

        string log = $"\n[Итерация {goldIter}]\n" +
                     $"a = {Math.Round(goldA, 4)}, b = {Math.Round(goldB, 4)}\n" +
                     $"x1 = {Math.Round(goldX1, 4)}, f(x1) = {Math.Round(goldF1, 4)}\n" +
                     $"x2 = {Math.Round(goldX2, 4)}, f(x2) = {Math.Round(goldF2, 4)}\n";

        chart1.Series["Интервал"].Points.Clear();
        chart1.Series["Интервал"].Points.AddXY(goldA, 0);
        chart1.Series["Интервал"].Points.AddXY(goldB, 0);

        chart1.Series["Экстремум"].Points.Clear();
        chart1.Series["Экстремум"].Points.AddXY(goldX1, goldF1);
        chart1.Series["Корень"].Points.Clear();
        chart1.Series["Корень"].Points.AddXY(goldX2, goldF2);

        bool condition = goldIsMax ? (goldF1 > goldF2) : (goldF1 < goldF2);
        if (condition)
        {
          log += "-> f(x1) лучше, сдвигаем b = x2";
          goldB = goldX2;
          goldX2 = goldX1; goldF2 = goldF1;
          goldX1 = goldA + Resphi * (goldB - goldA);
          goldF1 = EvaluateFunction(formula, goldX1);
        }
        else
        {
          log += "-> f(x2) лучше, сдвигаем a = x1";
          goldA = goldX1;
          goldX1 = goldX2; goldF1 = goldF2;
          goldX2 = goldB - Resphi * (goldB - goldA);
          goldF2 = EvaluateFunction(formula, goldX2);
        }
        lblResult.Text += log;
      }

      // Правильная прокрутка панели (исправление ошибки CS1061)
      if (lblResult.Parent is ScrollableControl scrollableParent)
      {
        scrollableParent.ScrollControlIntoView(lblResult);
      }
    }

    private void FinishAnimation(string message)
    {
      animTimer.Stop();
      isAnimating = false;
      pauseMenuItem.Enabled = false;
      pauseMenuItem.Text = "Пауза / Продолжить";
      lblResult.Text += "\n\n=== ЗАВЕРШЕНО ===\n" + message;
    }

    // ================= ОСТАЛЬНАЯ ЛОГИКА =================

    private void CalcMenuItem_Click(object sender, EventArgs e)
    {
      animTimer.Stop();
      isAnimating = false;
      pauseMenuItem.Enabled = false;
      if (!ValidateInputs(out double a, out double b, out double eps, out string formula)) return;

      int decimals = GetDecimalPlaces(eps);
      chart1.Series["Корень"].Points.Clear();
      chart1.Series["Экстремум"].Points.Clear();
      chart1.Series["Интервал"].Points.Clear();
      DrawGraph(formula, a, b);

      string method = methodComboBox.SelectedItem.ToString();

      if (method.Contains("Дихотомия"))
        lblResult.Text = CalculateDichotomyFinal(formula, a, b, eps, decimals);
      else
        lblResult.Text = CalculateAllExtremums(formula, a, b, eps, decimals);
    }

    private void ClearMenuItem_Click(object sender, EventArgs e)
    {
      animTimer.Stop();
      isAnimating = false;
      isPaused = false;
      pauseMenuItem.Enabled = false;
      txtA.Text = "-5";
      txtB.Text = "5";
      txtE.Text = "0,1";
      txtFormula.Text = "x^3 - 3*x + 1";
      lblResult.Text = "";
      foreach (var series in chart1.Series) series.Points.Clear();
    }

    private void PlotMenuItem_Click(object sender, EventArgs e)
    {
      if (ValidateInputs(out double a, out double b, out double eps, out string formula))
        DrawGraph(formula, a, b);
    }

    private double EvaluateFunction(string formula, double xValue)
    {
      Argument x = new Argument("x", xValue);
      Expression e = new Expression(formula, x);
      return e.calculate();
    }

    private bool ValidateInputs(out double a, out double b, out double eps, out string formula)
    {
      a = b = eps = 0;
      formula = txtFormula.Text;

      if (txtA.Text.Contains(" ") || txtB.Text.Contains(" ") || txtE.Text.Contains(" "))
      { MessageBox.Show("Пробелы запрещены!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }

      if (IsLeadingZero(txtA.Text) || IsLeadingZero(txtB.Text) || IsLeadingZero(txtE.Text))
      { MessageBox.Show("Числа не могут начинаться с нуля!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }

      if (string.IsNullOrWhiteSpace(formula))
      { MessageBox.Show("Введите функцию!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }

      if (!double.TryParse(txtA.Text.Replace('.', ','), out a) ||
          !double.TryParse(txtB.Text.Replace('.', ','), out b) ||
          !double.TryParse(txtE.Text.Replace('.', ','), out eps))
      { MessageBox.Show("Некорректный ввод.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }

      if (a >= b) { MessageBox.Show("'a' должно быть меньше 'b'.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }
      if (eps <= 0) { MessageBox.Show("Погрешность > 0.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }
      return true;
    }

    private bool IsLeadingZero(string input)
    {
      if (string.IsNullOrEmpty(input)) return false;
      input = input.Trim();
      if (input.Length > 1 && input[0] == '0')
      { if (input[1] != ',' && input[1] != '.') return true; }
      return false;
    }

    private void DrawGraph(string formula, double a, double b)
    {
      chart1.Series["F(x)"].Points.Clear();
      double step = (b - a) / 500.0;
      for (double x = a; x <= b; x += step)
      {
        double y = EvaluateFunction(formula, x);
        if (!double.IsNaN(y) && !double.IsInfinity(y) && Math.Abs(y) < 1000)
          chart1.Series["F(x)"].Points.AddXY(x, y);
      }
      chart1.ChartAreas[0].RecalculateAxesScale();
    }

    private int GetDecimalPlaces(double eps)
    {
      int places = 0;
      while (eps < 1 && places < 10) { eps *= 10; places++; }
      return places;
    }

    private string CalculateDichotomyFinal(string formula, double a, double b, double eps, int decimals)
    {
      double left = a, right = b, x0 = 0, fx0 = 0;
      int iter = 0;
      while (Math.Abs(right - left) > eps && iter < 10000)
      {
        x0 = (left + right) / 2.0; fx0 = EvaluateFunction(formula, x0);
        if (EvaluateFunction(formula, left) * fx0 < 0) right = x0; else left = x0;
        iter++;
      }
      chart1.Series["Корень"].Points.AddXY(x0, fx0);
      return $"[ДИХОТОМИЯ]\nКорень: x = {Math.Round(x0, decimals)}\nf(x) = {Math.Round(fx0, decimals + 2)}\nИтераций: {iter}";
    }

    private string CalculateAllExtremums(string formula, double a, double b, double eps, int decimals)
    {
      List<double> resX = new List<double>();
      List<double> resY = new List<double>();
      double scanStep = (b - a) / 100.0;
      for (double x = a; x <= b; x += scanStep)
      {
        double y = EvaluateFunction(formula, x);
        double yL = EvaluateFunction(formula, x - 0.01);
        double yR = EvaluateFunction(formula, x + 0.01);
        if ((y < yL && y < yR) || (y > yL && y > yR))
        {
          double rx = RefineExtremum(formula, x - scanStep, x + scanStep, eps);
          double ry = EvaluateFunction(formula, rx);
          bool dup = false;
          foreach (var v in resX) if (Math.Abs(v - rx) < eps * 10) dup = true;
          if (!dup) { resX.Add(rx); resY.Add(ry); chart1.Series["Экстремум"].Points.AddXY(rx, ry); }
        }
      }
      if (resX.Count == 0) return "Экстремумы не найдены.";
      string outStr = "[ЭКСТРЕМУМЫ]\n";
      for (int i = 0; i < resX.Count; i++)
      {
        string type = (resY[i] < EvaluateFunction(formula, resX[i] - 0.01)) ? "Мин" : "Макс";
        outStr += $"{i + 1}) {type}: x = {Math.Round(resX[i], 4)}, f(x) = {Math.Round(resY[i], 4)}\n";
      }
      return outStr;
    }

    private double RefineExtremum(string formula, double a, double b, double eps)
    {
      double x1 = a + Resphi * (b - a);
      double x2 = b - Resphi * (b - a);
      double f1 = EvaluateFunction(formula, x1);
      double f2 = EvaluateFunction(formula, x2);
      bool isMax = EvaluateFunction(formula, a) > EvaluateFunction(formula, b);
      int iter = 0;
      while (Math.Abs(b - a) > eps && iter < 100)
      {
        bool condition = isMax ? (f1 > f2) : (f1 < f2);
        if (condition) { b = x2; x2 = x1; f2 = f1; x1 = a + Resphi * (b - a); f1 = EvaluateFunction(formula, x1); }
        else { a = x1; x1 = x2; f1 = f2; x2 = b - Resphi * (b - a); f2 = EvaluateFunction(formula, x2); }
        iter++;
      }
      return (a + b) / 2;
    }

    private void SetupChartSeries()
    {
      chart1.Series.Clear();

      Series lineSeries = new Series("F(x)");
      lineSeries.ChartType = SeriesChartType.Spline;
      lineSeries.BorderWidth = 2;
      lineSeries.Color = Color.Blue;
      chart1.Series.Add(lineSeries);

      Series rootSeries = new Series("Корень");
      rootSeries.ChartType = SeriesChartType.Point;
      rootSeries.MarkerSize = 12;
      rootSeries.MarkerStyle = MarkerStyle.Circle;
      rootSeries.Color = Color.Red;
      chart1.Series.Add(rootSeries);

      Series extremumSeries = new Series("Экстремум");
      extremumSeries.ChartType = SeriesChartType.Point;
      extremumSeries.MarkerSize = 12;
      extremumSeries.MarkerStyle = MarkerStyle.Star5;
      extremumSeries.Color = Color.Green;
      chart1.Series.Add(extremumSeries);

      Series intervalSeries = new Series("Интервал");
      intervalSeries.ChartType = SeriesChartType.Line;
      intervalSeries.BorderWidth = 4;
      intervalSeries.Color = Color.Orange;
      chart1.Series.Add(intervalSeries);
    }
  }
}