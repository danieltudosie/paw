using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Xml.Linq;
using Microsoft.Data.Sqlite;
using SalesManagerApp.Models;

namespace SalesManagerApp
{
    public class MainForm : Form
    {
        private BindingList<Product> _products = new BindingList<Product>();
        private BindingList<Customer> _customers = new BindingList<Customer>();
        private BindingList<SaleTransaction> _transactions = new BindingList<SaleTransaction>();

        private BindingSource _productSource = new BindingSource();
        private BindingSource _customerSource = new BindingSource();
        private BindingSource _transactionSource = new BindingSource();

        private DataGridView _gridProducts;
        private DataGridView _gridCustomers;
        private DataGridView _gridTransactions;
        private TextBox _txtProductName;
        private TextBox _txtProductCategory;
        private NumericUpDown _numProductPrice;
        private NumericUpDown _numProductStock;
        private TextBox _txtCustomerName;
        private TextBox _txtCustomerEmail;
        private TextBox _txtCustomerPhone;
        private NumericUpDown _numCustomerPoints;
        private ComboBox _cmbProducts;
        private ComboBox _cmbCustomers;
        private NumericUpDown _numQuantity;
        private DateTimePicker _dtpTransactionDate;
        private Chart _chart;
        private Label _lblRevenue;
        private Label _lblBestCustomer;
        private Label _lblLowStock;
        private Label _lblExpensiveProduct;
        private Label _lblForeach;
        private PrintDocument _printDocument;

        private string _txtPath;
        private string _binPath;
        private string _configXmlPath;
        private string _exportXmlPath;
        private string _databasePath;
        private int _lowStockLimit;

        public MainForm()
        {
            PreparePaths();
            LoadConfigurationXml();
            InitializeDatabase();
            LoadAllDataFromDatabase();

            if (_products.Count == 0 && _customers.Count == 0 && _transactions.Count == 0)
            {
                LoadDemoData();
                SaveAllDataToDatabase();
            }

            InitializeComponent();
            BindControls();
            RefreshStatistics();
        }

        private void PreparePaths()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string dataDirectory = Path.Combine(baseDirectory, "Data");
            string configDirectory = Path.Combine(baseDirectory, "Config");

            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }

            if (!Directory.Exists(configDirectory))
            {
                Directory.CreateDirectory(configDirectory);
            }

            _txtPath = Path.Combine(dataDirectory, "data.txt");
            _binPath = Path.Combine(dataDirectory, "data.bin");
            _configXmlPath = Path.Combine(configDirectory, "app-config.xml");
            _exportXmlPath = Path.Combine(dataDirectory, "export.xml");
            _databasePath = Path.Combine(dataDirectory, "sales.db");
        }

        private void LoadConfigurationXml()
        {
            if (!File.Exists(_configXmlPath))
            {
                XDocument defaultDocument = new XDocument(
                    new XElement("config",
                        new XElement("title", "Gestiune Vanzari"),
                        new XElement("lowStockLimit", 5)));

                defaultDocument.Save(_configXmlPath);
            }

            XDocument document = XDocument.Load(_configXmlPath);
            XElement root = document.Element("config");

            if (root == null)
            {
                Text = "Gestiune Vanzari";
                _lowStockLimit = 5;
                return;
            }

            Text = root.Element("title") != null ? root.Element("title").Value : "Gestiune Vanzari";
            int parsedLimit;
            if (root.Element("lowStockLimit") != null && int.TryParse(root.Element("lowStockLimit").Value, out parsedLimit))
            {
                _lowStockLimit = parsedLimit;
            }
            else
            {
                _lowStockLimit = 5;
            }
        }

        private void InitializeComponent()
        {
            Width = 1300;
            Height = 800;
            StartPosition = FormStartPosition.CenterScreen;

            _printDocument = new PrintDocument();
            _printDocument.PrintPage += PrintDocument_PrintPage;

            MenuStrip menuStrip = BuildMenu();
            MainMenuStrip = menuStrip;

            TabControl tabControl = new TabControl();
            tabControl.Dock = DockStyle.Fill;
            tabControl.TabPages.Add(BuildProductsPage());
            tabControl.TabPages.Add(BuildCustomersPage());
            tabControl.TabPages.Add(BuildTransactionsPage());
            tabControl.TabPages.Add(BuildStatisticsPage());

            Controls.Add(tabControl);
            Controls.Add(menuStrip);
        }

        private MenuStrip BuildMenu()
        {
            MenuStrip menuStrip = new MenuStrip();

            ToolStripMenuItem fileMenu = new ToolStripMenuItem("Fisier");
            fileMenu.DropDownItems.Add(CreateMenuItem("Salvare TXT", SaveText_Click));
            fileMenu.DropDownItems.Add(CreateMenuItem("Restaurare TXT", LoadText_Click));
            fileMenu.DropDownItems.Add(CreateMenuItem("Salvare Binar", SaveBinary_Click));
            fileMenu.DropDownItems.Add(CreateMenuItem("Restaurare Binar", LoadBinary_Click));
            fileMenu.DropDownItems.Add(CreateMenuItem("Export XML", ExportXml_Click));
            fileMenu.DropDownItems.Add(CreateMenuItem("Print Preview", PrintPreview_Click));

            ToolStripMenuItem databaseMenu = new ToolStripMenuItem("Baza de date");
            databaseMenu.DropDownItems.Add(CreateMenuItem("Salveaza in baza de date", SaveDatabase_Click));
            databaseMenu.DropDownItems.Add(CreateMenuItem("Incarca din baza de date", LoadDatabase_Click));

            ToolStripMenuItem toolsMenu = new ToolStripMenuItem("Instrumente");
            toolsMenu.DropDownItems.Add(CreateMenuItem("Actualizeaza statistici", RefreshStatistics_Click));
            toolsMenu.DropDownItems.Add(CreateMenuItem("Cloneaza client selectat", CloneSelectedCustomer_Click));
            toolsMenu.DropDownItems.Add(CreateMenuItem("Creste stoc produs selectat", IncreaseSelectedStock_Click));

            menuStrip.Items.Add(fileMenu);
            menuStrip.Items.Add(databaseMenu);
            menuStrip.Items.Add(toolsMenu);
            return menuStrip;
        }

        private TabPage BuildProductsPage()
        {
            TabPage page = new TabPage("Produse");
            SplitContainer split = CreateSplitContainer();

            _gridProducts = CreateGrid();
            _gridProducts.SelectionChanged += GridProducts_SelectionChanged;
            _gridProducts.ContextMenuStrip = new ContextMenuStrip();
            _gridProducts.ContextMenuStrip.Items.Add("Sterge", null, DeleteProduct_Click);
            _gridProducts.ContextMenuStrip.Items.Add("Stoc +1", null, IncreaseSelectedStock_Click);
            split.Panel1.Controls.Add(_gridProducts);

            TableLayoutPanel panel = CreateEditorPanel(7);
            _txtProductName = new TextBox();
            _txtProductCategory = new TextBox();
            _numProductPrice = new NumericUpDown();
            _numProductPrice.DecimalPlaces = 2;
            _numProductPrice.Minimum = 1;
            _numProductPrice.Maximum = 100000;
            _numProductStock = new NumericUpDown();
            _numProductStock.Maximum = 10000;

            panel.Controls.Add(CreateLabel("Nume"), 0, 0);
            panel.Controls.Add(_txtProductName, 1, 0);
            panel.Controls.Add(CreateLabel("Categorie"), 0, 1);
            panel.Controls.Add(_txtProductCategory, 1, 1);
            panel.Controls.Add(CreateLabel("Pret"), 0, 2);
            panel.Controls.Add(_numProductPrice, 1, 2);
            panel.Controls.Add(CreateLabel("Stoc"), 0, 3);
            panel.Controls.Add(_numProductStock, 1, 3);
            panel.Controls.Add(CreateButton("Adauga", AddProduct_Click), 0, 4);
            panel.Controls.Add(CreateButton("Modifica", UpdateProduct_Click), 1, 4);
            panel.Controls.Add(CreateButton("Sterge", DeleteProduct_Click), 0, 5);
            panel.Controls.Add(CreateButton("Goleste", ClearProductFields_Click), 1, 5);

            split.Panel2.Controls.Add(panel);
            page.Controls.Add(split);
            return page;
        }

        private TabPage BuildCustomersPage()
        {
            TabPage page = new TabPage("Clienti");
            SplitContainer split = CreateSplitContainer();

            _gridCustomers = CreateGrid();
            _gridCustomers.SelectionChanged += GridCustomers_SelectionChanged;
            _gridCustomers.ContextMenuStrip = new ContextMenuStrip();
            _gridCustomers.ContextMenuStrip.Items.Add("Sterge", null, DeleteCustomer_Click);
            _gridCustomers.ContextMenuStrip.Items.Add("Cloneaza", null, CloneSelectedCustomer_Click);
            split.Panel1.Controls.Add(_gridCustomers);

            TableLayoutPanel panel = CreateEditorPanel(7);
            _txtCustomerName = new TextBox();
            _txtCustomerEmail = new TextBox();
            _txtCustomerPhone = new TextBox();
            _numCustomerPoints = new NumericUpDown();
            _numCustomerPoints.Maximum = 100000;

            panel.Controls.Add(CreateLabel("Nume"), 0, 0);
            panel.Controls.Add(_txtCustomerName, 1, 0);
            panel.Controls.Add(CreateLabel("Email"), 0, 1);
            panel.Controls.Add(_txtCustomerEmail, 1, 1);
            panel.Controls.Add(CreateLabel("Telefon"), 0, 2);
            panel.Controls.Add(_txtCustomerPhone, 1, 2);
            panel.Controls.Add(CreateLabel("Puncte"), 0, 3);
            panel.Controls.Add(_numCustomerPoints, 1, 3);
            panel.Controls.Add(CreateButton("Adauga", AddCustomer_Click), 0, 4);
            panel.Controls.Add(CreateButton("Modifica", UpdateCustomer_Click), 1, 4);
            panel.Controls.Add(CreateButton("Sterge", DeleteCustomer_Click), 0, 5);
            panel.Controls.Add(CreateButton("Goleste", ClearCustomerFields_Click), 1, 5);

            split.Panel2.Controls.Add(panel);
            page.Controls.Add(split);
            return page;
        }

        private TabPage BuildTransactionsPage()
        {
            TabPage page = new TabPage("Tranzactii");
            SplitContainer split = CreateSplitContainer();

            _gridTransactions = CreateGrid();
            _gridTransactions.ContextMenuStrip = new ContextMenuStrip();
            _gridTransactions.ContextMenuStrip.Items.Add("Sterge", null, DeleteTransaction_Click);
            split.Panel1.Controls.Add(_gridTransactions);

            TableLayoutPanel panel = CreateEditorPanel(6);
            _cmbProducts = new ComboBox();
            _cmbProducts.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbCustomers = new ComboBox();
            _cmbCustomers.DropDownStyle = ComboBoxStyle.DropDownList;
            _numQuantity = new NumericUpDown();
            _numQuantity.Minimum = 1;
            _numQuantity.Maximum = 1000;
            _dtpTransactionDate = new DateTimePicker();
            _dtpTransactionDate.Format = DateTimePickerFormat.Short;

            panel.Controls.Add(CreateLabel("Produs"), 0, 0);
            panel.Controls.Add(_cmbProducts, 1, 0);
            panel.Controls.Add(CreateLabel("Client"), 0, 1);
            panel.Controls.Add(_cmbCustomers, 1, 1);
            panel.Controls.Add(CreateLabel("Cantitate"), 0, 2);
            panel.Controls.Add(_numQuantity, 1, 2);
            panel.Controls.Add(CreateLabel("Data"), 0, 3);
            panel.Controls.Add(_dtpTransactionDate, 1, 3);
            panel.Controls.Add(CreateButton("Adauga tranzactie", AddTransaction_Click), 0, 4);
            panel.Controls.Add(CreateButton("Sterge tranzactie", DeleteTransaction_Click), 1, 4);

            split.Panel2.Controls.Add(panel);
            page.Controls.Add(split);
            return page;
        }

        private TabPage BuildStatisticsPage()
        {
            TabPage page = new TabPage("Statistici");
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.RowCount = 2;
            layout.ColumnCount = 1;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            FlowLayoutPanel infoPanel = new FlowLayoutPanel();
            infoPanel.Dock = DockStyle.Fill;
            infoPanel.Padding = new Padding(15);
            infoPanel.AutoScroll = true;

            _lblRevenue = CreateInfoLabel();
            _lblBestCustomer = CreateInfoLabel();
            _lblLowStock = CreateInfoLabel();
            _lblExpensiveProduct = CreateInfoLabel();
            _lblForeach = CreateInfoLabel();

            infoPanel.Controls.Add(_lblRevenue);
            infoPanel.Controls.Add(_lblBestCustomer);
            infoPanel.Controls.Add(_lblLowStock);
            infoPanel.Controls.Add(_lblExpensiveProduct);
            infoPanel.Controls.Add(_lblForeach);

            _chart = new Chart();
            _chart.Dock = DockStyle.Fill;
            _chart.ChartAreas.Add(new ChartArea("MainArea"));

            layout.Controls.Add(infoPanel, 0, 0);
            layout.Controls.Add(_chart, 0, 1);
            page.Controls.Add(layout);
            return page;
        }

        private void BindControls()
        {
            _productSource.DataSource = _products;
            _customerSource.DataSource = _customers;
            _transactionSource.DataSource = _transactions;

            _gridProducts.DataSource = _productSource;
            _gridCustomers.DataSource = _customerSource;
            _gridTransactions.DataSource = _transactionSource;

            _cmbProducts.DataSource = _productSource;
            _cmbProducts.DisplayMember = "Name";
            _cmbProducts.ValueMember = "Id";

            _cmbCustomers.DataSource = _customerSource;
            _cmbCustomers.DisplayMember = "FullName";
            _cmbCustomers.ValueMember = "Id";
        }

        private void AddProduct_Click(object sender, EventArgs e)
        {
            SaveProduct(false);
        }

        private void UpdateProduct_Click(object sender, EventArgs e)
        {
            SaveProduct(true);
        }

        private void DeleteProduct_Click(object sender, EventArgs e)
        {
            try
            {
                Product product = GetSelectedProduct();
                if (product == null)
                {
                    return;
                }

                if (_transactions.Any(t => t.ProductId == product.Id))
                {
                    throw new InvalidOperationException("Produsul are tranzactii asociate.");
                }

                _products.Remove(product);
                SaveAllDataToDatabase();
                RefreshStatistics();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
            }
        }

        private void ClearProductFields_Click(object sender, EventArgs e)
        {
            _txtProductName.Text = string.Empty;
            _txtProductCategory.Text = string.Empty;
            _numProductPrice.Value = 1;
            _numProductStock.Value = 0;
        }

        private void AddCustomer_Click(object sender, EventArgs e)
        {
            SaveCustomer(false);
        }

        private void UpdateCustomer_Click(object sender, EventArgs e)
        {
            SaveCustomer(true);
        }

        private void DeleteCustomer_Click(object sender, EventArgs e)
        {
            try
            {
                Customer customer = GetSelectedCustomer();
                if (customer == null)
                {
                    return;
                }

                if (_transactions.Any(t => t.CustomerId == customer.Id))
                {
                    throw new InvalidOperationException("Clientul are tranzactii asociate.");
                }

                _customers.Remove(customer);
                SaveAllDataToDatabase();
                RefreshStatistics();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
            }
        }

        private void ClearCustomerFields_Click(object sender, EventArgs e)
        {
            _txtCustomerName.Text = string.Empty;
            _txtCustomerEmail.Text = string.Empty;
            _txtCustomerPhone.Text = string.Empty;
            _numCustomerPoints.Value = 0;
        }

        private void AddTransaction_Click(object sender, EventArgs e)
        {
            try
            {
                Product product = _cmbProducts.SelectedItem as Product;
                Customer customer = _cmbCustomers.SelectedItem as Customer;

                if (product == null || customer == null)
                {
                    throw new InvalidOperationException("Selectati produsul si clientul.");
                }

                SaleTransaction transaction = new SaleTransaction(
                    GetNextTransactionId(),
                    product.Id,
                    product.Name,
                    customer.Id,
                    customer.FullName,
                    Convert.ToInt32(_numQuantity.Value),
                    product.Price,
                    _dtpTransactionDate.Value.Date);

                ValidateEntity(transaction);

                if (product.Stock < transaction.Quantity)
                {
                    throw new InvalidOperationException("Stoc insuficient.");
                }

                product.Stock -= transaction.Quantity;
                customer.LoyaltyPoints += transaction.Quantity;
                product[transaction.Date.Month - 1] = product[transaction.Date.Month - 1] + transaction.Quantity;

                _transactions.Add(transaction);
                SaveAllDataToDatabase();
                RefreshDataBindings();
                RefreshStatistics();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
            }
        }

        private void DeleteTransaction_Click(object sender, EventArgs e)
        {
            SaleTransaction transaction = GetSelectedTransaction();
            if (transaction == null)
            {
                return;
            }

            Product product = _products.FirstOrDefault(p => p.Id == transaction.ProductId);
            Customer customer = _customers.FirstOrDefault(c => c.Id == transaction.CustomerId);

            if (product != null)
            {
                product.Stock += transaction.Quantity;
                int currentValue = product[transaction.Date.Month - 1];
                product[transaction.Date.Month - 1] = Math.Max(0, currentValue - transaction.Quantity);
            }

            if (customer != null)
            {
                customer.LoyaltyPoints = Math.Max(0, customer.LoyaltyPoints - transaction.Quantity);
            }

            _transactions.Remove(transaction);
            SaveAllDataToDatabase();
            RefreshDataBindings();
            RefreshStatistics();
        }

        private void RefreshStatistics_Click(object sender, EventArgs e)
        {
            RefreshStatistics();
        }

        private void CloneSelectedCustomer_Click(object sender, EventArgs e)
        {
            Customer customer = GetSelectedCustomer();
            if (customer == null)
            {
                return;
            }

            Customer copy = (Customer)customer.Clone();
            copy.Id = GetNextCustomerId();
            copy.FullName = copy.FullName + " Copie";
            _customers.Add(copy);
            SaveAllDataToDatabase();
            RefreshDataBindings();
            RefreshStatistics();
        }

        private void IncreaseSelectedStock_Click(object sender, EventArgs e)
        {
            Product product = GetSelectedProduct();
            if (product == null)
            {
                return;
            }

            Product changed = product + 1;
            changed.Id = product.Id;
            CopyProductValues(product, changed);
            SaveAllDataToDatabase();
            RefreshDataBindings();
            RefreshStatistics();
        }

        private void SaveText_Click(object sender, EventArgs e)
        {
            SaveDataToTextFile();
            MessageBox.Show("Datele au fost salvate in fisier TXT.");
        }

        private void LoadText_Click(object sender, EventArgs e)
        {
            if (!File.Exists(_txtPath))
            {
                MessageBox.Show("Fisierul TXT nu exista.");
                return;
            }

            LoadDataFromTextFile();
            SaveAllDataToDatabase();
            RefreshDataBindings();
            RefreshStatistics();
        }

        private void SaveBinary_Click(object sender, EventArgs e)
        {
            SaveDataToBinaryFile();
            MessageBox.Show("Datele au fost salvate in fisier binar.");
        }

        private void LoadBinary_Click(object sender, EventArgs e)
        {
            if (!File.Exists(_binPath))
            {
                MessageBox.Show("Fisierul binar nu exista.");
                return;
            }

            LoadDataFromBinaryFile();
            SaveAllDataToDatabase();
            RefreshDataBindings();
            RefreshStatistics();
        }

        private void ExportXml_Click(object sender, EventArgs e)
        {
            ExportDataToXml();
            MessageBox.Show("Exportul XML a fost realizat.");
        }

        private void PrintPreview_Click(object sender, EventArgs e)
        {
            PrintPreviewDialog previewDialog = new PrintPreviewDialog();
            previewDialog.Document = _printDocument;
            previewDialog.Width = 900;
            previewDialog.Height = 700;
            previewDialog.ShowDialog(this);
        }

        private void SaveDatabase_Click(object sender, EventArgs e)
        {
            SaveAllDataToDatabase();
            MessageBox.Show("Datele au fost salvate in baza de date.");
        }

        private void LoadDatabase_Click(object sender, EventArgs e)
        {
            LoadAllDataFromDatabase();
            RefreshDataBindings();
            RefreshStatistics();
            MessageBox.Show("Datele au fost incarcate din baza de date.");
        }

        private void GridProducts_SelectionChanged(object sender, EventArgs e)
        {
            Product product = GetSelectedProduct();
            if (product == null)
            {
                return;
            }

            _txtProductName.Text = product.Name;
            _txtProductCategory.Text = product.Category;
            _numProductPrice.Value = product.Price;
            _numProductStock.Value = product.Stock;
        }

        private void GridCustomers_SelectionChanged(object sender, EventArgs e)
        {
            Customer customer = GetSelectedCustomer();
            if (customer == null)
            {
                return;
            }

            _txtCustomerName.Text = customer.FullName;
            _txtCustomerEmail.Text = customer.Email;
            _txtCustomerPhone.Text = customer.Phone;
            _numCustomerPoints.Value = customer.LoyaltyPoints;
        }

        private void SaveProduct(bool updateExisting)
        {
            try
            {
                int id = updateExisting && GetSelectedProduct() != null ? GetSelectedProduct().Id : GetNextProductId();
                Product product = new Product(id, _txtProductName.Text, _txtProductCategory.Text, _numProductPrice.Value, Convert.ToInt32(_numProductStock.Value));
                ValidateEntity(product);

                Product existing = _products.FirstOrDefault(p => p.Id == id);
                if (existing == null)
                {
                    _products.Add(product);
                }
                else
                {
                    Product cloned = (Product)existing.Clone();
                    CopyProductValues(existing, product);
                    CopyMonthlySold(existing, cloned);
                }

                SaveAllDataToDatabase();
                RefreshDataBindings();
                RefreshStatistics();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
            }
        }

        private void SaveCustomer(bool updateExisting)
        {
            try
            {
                int id = updateExisting && GetSelectedCustomer() != null ? GetSelectedCustomer().Id : GetNextCustomerId();
                Customer customer = new Customer(id, _txtCustomerName.Text, _txtCustomerEmail.Text, _txtCustomerPhone.Text, Convert.ToInt32(_numCustomerPoints.Value));
                ValidateEntity(customer);

                Customer existing = _customers.FirstOrDefault(c => c.Id == id);
                if (existing == null)
                {
                    _customers.Add(customer);
                }
                else
                {
                    existing.FullName = customer.FullName;
                    existing.Email = customer.Email;
                    existing.Phone = customer.Phone;
                    existing.LoyaltyPoints = customer.LoyaltyPoints;
                }

                SaveAllDataToDatabase();
                RefreshDataBindings();
                RefreshStatistics();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
            }
        }

        private void RefreshStatistics()
        {
            decimal totalRevenue = 0;
            foreach (SaleTransaction transaction in _transactions)
            {
                totalRevenue += (decimal)transaction;
            }

            List<Customer> sortedCustomers = _customers.Select(c => (Customer)c.Clone()).ToList();
            sortedCustomers.Sort();
            Customer bestCustomer = sortedCustomers.Count == 0 ? null : sortedCustomers[sortedCustomers.Count - 1];

            Product mostExpensiveProduct = null;
            foreach (Product product in _products)
            {
                if (mostExpensiveProduct == null || product > mostExpensiveProduct)
                {
                    mostExpensiveProduct = product;
                }
            }

            int lowStockCount = _products.Count(p => p.Stock <= _lowStockLimit);

            string foreachMessage = "Nu exista produse.";
            if (_products.Count > 0)
            {
                int soldUnits = 0;
                foreach (int value in _products[0])
                {
                    soldUnits += value;
                }

                foreachMessage = "Foreach pe indexator: " + _products[0].Name + " are " + soldUnits + " unitati vandute.";
            }

            _lblRevenue.Text = "Venit total: " + totalRevenue.ToString("0.00") + " lei";
            _lblBestCustomer.Text = "Cel mai bun client: " + (bestCustomer == null ? "-" : bestCustomer.FullName);
            _lblLowStock.Text = "Produse cu stoc mic: " + lowStockCount;
            _lblExpensiveProduct.Text = "Cel mai scump produs: " + (mostExpensiveProduct == null ? "-" : mostExpensiveProduct.Name);
            _lblForeach.Text = foreachMessage;

            decimal[] monthlyValues = new decimal[12];
            foreach (SaleTransaction transaction in _transactions)
            {
                monthlyValues[transaction.Date.Month - 1] += transaction.Total;
            }

            _chart.Series.Clear();
            Series series = new Series("Vanzari lunare");
            series.ChartType = SeriesChartType.Column;
            series.IsValueShownAsLabel = true;

            for (int i = 0; i < 12; i++)
            {
                series.Points.AddXY(CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedMonthNames[i], monthlyValues[i]);
            }

            _chart.Series.Add(series);
        }

        private void SaveDataToTextFile()
        {
            List<string> lines = new List<string>();
            lines.Add("[PRODUCTS]");

            foreach (Product product in _products)
            {
                lines.Add(product.Id + "|" + product.Name + "|" + product.Category + "|" + product.Price.ToString(CultureInfo.InvariantCulture) + "|" + product.Stock);
            }

            lines.Add("[CUSTOMERS]");
            foreach (Customer customer in _customers)
            {
                lines.Add(customer.Id + "|" + customer.FullName + "|" + customer.Email + "|" + customer.Phone + "|" + customer.LoyaltyPoints);
            }

            lines.Add("[TRANSACTIONS]");
            foreach (SaleTransaction transaction in _transactions)
            {
                lines.Add(transaction.Id + "|" + transaction.ProductId + "|" + transaction.ProductName + "|" + transaction.CustomerId + "|" + transaction.CustomerName + "|" + transaction.Quantity + "|" + transaction.UnitPrice.ToString(CultureInfo.InvariantCulture) + "|" + transaction.Date.ToString("O"));
            }

            File.WriteAllLines(_txtPath, lines);
        }

        private void LoadDataFromTextFile()
        {
            _products.Clear();
            _customers.Clear();
            _transactions.Clear();

            string section = string.Empty;
            foreach (string line in File.ReadAllLines(_txtPath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.StartsWith("["))
                {
                    section = line;
                    continue;
                }

                string[] parts = line.Split('|');
                if (section == "[PRODUCTS]")
                {
                    _products.Add(new Product(
                        int.Parse(parts[0]),
                        parts[1],
                        parts[2],
                        decimal.Parse(parts[3], CultureInfo.InvariantCulture),
                        int.Parse(parts[4])));
                }
                else if (section == "[CUSTOMERS]")
                {
                    _customers.Add(new Customer(
                        int.Parse(parts[0]),
                        parts[1],
                        parts[2],
                        parts[3],
                        int.Parse(parts[4])));
                }
                else if (section == "[TRANSACTIONS]")
                {
                    _transactions.Add(new SaleTransaction(
                        int.Parse(parts[0]),
                        int.Parse(parts[1]),
                        parts[2],
                        int.Parse(parts[3]),
                        parts[4],
                        int.Parse(parts[5]),
                        decimal.Parse(parts[6], CultureInfo.InvariantCulture),
                        DateTime.Parse(parts[7], null, DateTimeStyles.RoundtripKind)));
                }
            }

            RebuildMonthlySalesFromTransactions();
        }

        private void SaveDataToBinaryFile()
        {
            using (FileStream stream = File.Create(_binPath))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(_products.Count);
                foreach (Product product in _products)
                {
                    writer.Write(product.Id);
                    writer.Write(product.Name);
                    writer.Write(product.Category);
                    writer.Write(product.Price.ToString(CultureInfo.InvariantCulture));
                    writer.Write(product.Stock);
                }

                writer.Write(_customers.Count);
                foreach (Customer customer in _customers)
                {
                    writer.Write(customer.Id);
                    writer.Write(customer.FullName);
                    writer.Write(customer.Email);
                    writer.Write(customer.Phone);
                    writer.Write(customer.LoyaltyPoints);
                }

                writer.Write(_transactions.Count);
                foreach (SaleTransaction transaction in _transactions)
                {
                    writer.Write(transaction.Id);
                    writer.Write(transaction.ProductId);
                    writer.Write(transaction.ProductName);
                    writer.Write(transaction.CustomerId);
                    writer.Write(transaction.CustomerName);
                    writer.Write(transaction.Quantity);
                    writer.Write(transaction.UnitPrice.ToString(CultureInfo.InvariantCulture));
                    writer.Write(transaction.Date.ToBinary());
                }
            }
        }

        private void LoadDataFromBinaryFile()
        {
            _products.Clear();
            _customers.Clear();
            _transactions.Clear();

            using (FileStream stream = File.OpenRead(_binPath))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                int productCount = reader.ReadInt32();
                for (int i = 0; i < productCount; i++)
                {
                    _products.Add(new Product(
                        reader.ReadInt32(),
                        reader.ReadString(),
                        reader.ReadString(),
                        decimal.Parse(reader.ReadString(), CultureInfo.InvariantCulture),
                        reader.ReadInt32()));
                }

                int customerCount = reader.ReadInt32();
                for (int i = 0; i < customerCount; i++)
                {
                    _customers.Add(new Customer(
                        reader.ReadInt32(),
                        reader.ReadString(),
                        reader.ReadString(),
                        reader.ReadString(),
                        reader.ReadInt32()));
                }

                int transactionCount = reader.ReadInt32();
                for (int i = 0; i < transactionCount; i++)
                {
                    _transactions.Add(new SaleTransaction(
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        reader.ReadString(),
                        reader.ReadInt32(),
                        reader.ReadString(),
                        reader.ReadInt32(),
                        decimal.Parse(reader.ReadString(), CultureInfo.InvariantCulture),
                        DateTime.FromBinary(reader.ReadInt64())));
                }
            }

            RebuildMonthlySalesFromTransactions();
        }

        private void ExportDataToXml()
        {
            XDocument document = new XDocument(
                new XElement("storeData",
                    new XElement("products",
                        _products.Select(product =>
                            new XElement("product",
                                new XAttribute("id", product.Id),
                                new XElement("name", product.Name),
                                new XElement("category", product.Category),
                                new XElement("price", product.Price),
                                new XElement("stock", product.Stock)))),
                    new XElement("customers",
                        _customers.Select(customer =>
                            new XElement("customer",
                                new XAttribute("id", customer.Id),
                                new XElement("fullName", customer.FullName),
                                new XElement("email", customer.Email),
                                new XElement("phone", customer.Phone),
                                new XElement("loyaltyPoints", customer.LoyaltyPoints)))),
                    new XElement("transactions",
                        _transactions.Select(transaction =>
                            new XElement("transaction",
                                new XAttribute("id", transaction.Id),
                                new XElement("productId", transaction.ProductId),
                                new XElement("productName", transaction.ProductName),
                                new XElement("customerId", transaction.CustomerId),
                                new XElement("customerName", transaction.CustomerName),
                                new XElement("quantity", transaction.Quantity),
                                new XElement("unitPrice", transaction.UnitPrice),
                                new XElement("date", transaction.Date))))));

            document.Save(_exportXmlPath);
        }

        private void InitializeDatabase()
        {
            using (SqliteConnection connection = new SqliteConnection("Data Source=" + _databasePath))
            {
                connection.Open();
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText =
                        "CREATE TABLE IF NOT EXISTS Products (" +
                        "Id INTEGER PRIMARY KEY, Name TEXT NOT NULL, Category TEXT NOT NULL, Price REAL NOT NULL, Stock INTEGER NOT NULL);" +
                        "CREATE TABLE IF NOT EXISTS Customers (" +
                        "Id INTEGER PRIMARY KEY, FullName TEXT NOT NULL, Email TEXT NOT NULL, Phone TEXT NOT NULL, LoyaltyPoints INTEGER NOT NULL);" +
                        "CREATE TABLE IF NOT EXISTS Transactions (" +
                        "Id INTEGER PRIMARY KEY, ProductId INTEGER NOT NULL, ProductName TEXT NOT NULL, CustomerId INTEGER NOT NULL, CustomerName TEXT NOT NULL, Quantity INTEGER NOT NULL, UnitPrice REAL NOT NULL, Date TEXT NOT NULL);";
                    command.ExecuteNonQuery();
                }
            }
        }

        private void SaveAllDataToDatabase()
        {
            using (SqliteConnection connection = new SqliteConnection("Data Source=" + _databasePath))
            {
                connection.Open();

                ExecuteNonQuery(connection, "DELETE FROM Transactions;");
                ExecuteNonQuery(connection, "DELETE FROM Products;");
                ExecuteNonQuery(connection, "DELETE FROM Customers;");

                foreach (Product product in _products)
                {
                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.CommandText = "INSERT INTO Products(Id, Name, Category, Price, Stock) VALUES($id, $name, $category, $price, $stock);";
                        command.Parameters.AddWithValue("$id", product.Id);
                        command.Parameters.AddWithValue("$name", product.Name);
                        command.Parameters.AddWithValue("$category", product.Category);
                        command.Parameters.AddWithValue("$price", product.Price);
                        command.Parameters.AddWithValue("$stock", product.Stock);
                        command.ExecuteNonQuery();
                    }
                }

                foreach (Customer customer in _customers)
                {
                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.CommandText = "INSERT INTO Customers(Id, FullName, Email, Phone, LoyaltyPoints) VALUES($id, $fullName, $email, $phone, $points);";
                        command.Parameters.AddWithValue("$id", customer.Id);
                        command.Parameters.AddWithValue("$fullName", customer.FullName);
                        command.Parameters.AddWithValue("$email", customer.Email);
                        command.Parameters.AddWithValue("$phone", customer.Phone);
                        command.Parameters.AddWithValue("$points", customer.LoyaltyPoints);
                        command.ExecuteNonQuery();
                    }
                }

                foreach (SaleTransaction transaction in _transactions)
                {
                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.CommandText = "INSERT INTO Transactions(Id, ProductId, ProductName, CustomerId, CustomerName, Quantity, UnitPrice, Date) VALUES($id, $productId, $productName, $customerId, $customerName, $quantity, $unitPrice, $date);";
                        command.Parameters.AddWithValue("$id", transaction.Id);
                        command.Parameters.AddWithValue("$productId", transaction.ProductId);
                        command.Parameters.AddWithValue("$productName", transaction.ProductName);
                        command.Parameters.AddWithValue("$customerId", transaction.CustomerId);
                        command.Parameters.AddWithValue("$customerName", transaction.CustomerName);
                        command.Parameters.AddWithValue("$quantity", transaction.Quantity);
                        command.Parameters.AddWithValue("$unitPrice", transaction.UnitPrice);
                        command.Parameters.AddWithValue("$date", transaction.Date.ToString("O"));
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        private void LoadAllDataFromDatabase()
        {
            _products.Clear();
            _customers.Clear();
            _transactions.Clear();

            using (SqliteConnection connection = new SqliteConnection("Data Source=" + _databasePath))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT Id, Name, Category, Price, Stock FROM Products ORDER BY Id;";
                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _products.Add(new Product(
                                reader.GetInt32(0),
                                reader.GetString(1),
                                reader.GetString(2),
                                Convert.ToDecimal(reader.GetDouble(3)),
                                reader.GetInt32(4)));
                        }
                    }
                }

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT Id, FullName, Email, Phone, LoyaltyPoints FROM Customers ORDER BY Id;";
                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _customers.Add(new Customer(
                                reader.GetInt32(0),
                                reader.GetString(1),
                                reader.GetString(2),
                                reader.GetString(3),
                                reader.GetInt32(4)));
                        }
                    }
                }

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT Id, ProductId, ProductName, CustomerId, CustomerName, Quantity, UnitPrice, Date FROM Transactions ORDER BY Id;";
                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _transactions.Add(new SaleTransaction(
                                reader.GetInt32(0),
                                reader.GetInt32(1),
                                reader.GetString(2),
                                reader.GetInt32(3),
                                reader.GetString(4),
                                reader.GetInt32(5),
                                Convert.ToDecimal(reader.GetDouble(6)),
                                DateTime.Parse(reader.GetString(7))));
                        }
                    }
                }
            }

            RebuildMonthlySalesFromTransactions();
        }

        private void RebuildMonthlySalesFromTransactions()
        {
            foreach (Product product in _products)
            {
                for (int i = 0; i < 12; i++)
                {
                    product[i] = 0;
                }
            }

            foreach (SaleTransaction transaction in _transactions)
            {
                Product product = _products.FirstOrDefault(p => p.Id == transaction.ProductId);
                if (product != null)
                {
                    product[transaction.Date.Month - 1] = product[transaction.Date.Month - 1] + transaction.Quantity;
                }
            }
        }

        private void LoadDemoData()
        {
            Product product1 = new Product(1, "Laptop", "IT", 3200, 10);
            Product product2 = new Product(2, "Mouse", "IT", 90, 30);
            Product product3 = new Product(3, "Casti", "Audio", 180, 15);

            Customer customer1 = new Customer(1, "Popescu Andrei", "andrei@email.com", "0711111111", 2);
            Customer customer2 = new Customer(2, "Ionescu Maria", "maria@email.com", "0722222222", 5);
            Customer customer3 = new Customer(3, "Georgescu Elena", "elena@email.com", "0733333333", 1);

            _products.Add(product1);
            _products.Add(product2);
            _products.Add(product3);
            _customers.Add(customer1);
            _customers.Add(customer2);
            _customers.Add(customer3);

            SaleTransaction transaction1 = new SaleTransaction(1, 1, "Laptop", 1, "Popescu Andrei", 1, 3200, DateTime.Today.AddDays(-5));
            SaleTransaction transaction2 = new SaleTransaction(2, 2, "Mouse", 2, "Ionescu Maria", 2, 90, DateTime.Today.AddDays(-2));
            SaleTransaction transaction3 = new SaleTransaction(3, 3, "Casti", 3, "Georgescu Elena", 1, 180, DateTime.Today);

            _transactions.Add(transaction1);
            _transactions.Add(transaction2);
            _transactions.Add(transaction3);

            product1.Stock -= 1;
            product2.Stock -= 2;
            product3.Stock -= 1;
            customer1.LoyaltyPoints += 1;
            customer2.LoyaltyPoints += 2;
            customer3.LoyaltyPoints += 1;
            product1[transaction1.Date.Month - 1] += 1;
            product2[transaction2.Date.Month - 1] += 2;
            product3[transaction3.Date.Month - 1] += 1;
        }

        private void PrintDocument_PrintPage(object sender, PrintPageEventArgs e)
        {
            List<string> lines = new List<string>();
            lines.Add(Text);
            lines.Add("Numar produse: " + _products.Count);
            lines.Add("Numar clienti: " + _customers.Count);
            lines.Add("Numar tranzactii: " + _transactions.Count);
            lines.Add("Venit total: " + _transactions.Sum(t => t.Total).ToString("0.00") + " lei");

            Customer bestCustomer = _customers.OrderBy(c => c.LoyaltyPoints).LastOrDefault();
            lines.Add("Cel mai bun client: " + (bestCustomer == null ? "-" : bestCustomer.FullName));

            int y = 50;
            foreach (string line in lines)
            {
                e.Graphics.DrawString(line, new Font("Segoe UI", 12), Brushes.Black, 50, y);
                y += 30;
            }
        }

        private void ValidateEntity(EntityBase entity)
        {
            string validationMessage = entity.Validate();
            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                throw new InvalidOperationException(validationMessage);
            }
        }

        private int GetNextProductId()
        {
            return _products.Count == 0 ? 1 : _products.Max(p => p.Id) + 1;
        }

        private int GetNextCustomerId()
        {
            return _customers.Count == 0 ? 1 : _customers.Max(c => c.Id) + 1;
        }

        private int GetNextTransactionId()
        {
            return _transactions.Count == 0 ? 1 : _transactions.Max(t => t.Id) + 1;
        }

        private Product GetSelectedProduct()
        {
            return _gridProducts != null && _gridProducts.CurrentRow != null ? _gridProducts.CurrentRow.DataBoundItem as Product : null;
        }

        private Customer GetSelectedCustomer()
        {
            return _gridCustomers != null && _gridCustomers.CurrentRow != null ? _gridCustomers.CurrentRow.DataBoundItem as Customer : null;
        }

        private SaleTransaction GetSelectedTransaction()
        {
            return _gridTransactions != null && _gridTransactions.CurrentRow != null ? _gridTransactions.CurrentRow.DataBoundItem as SaleTransaction : null;
        }

        private void CopyProductValues(Product target, Product source)
        {
            target.Name = source.Name;
            target.Category = source.Category;
            target.Price = source.Price;
            target.Stock = source.Stock;
        }

        private void CopyMonthlySold(Product target, Product source)
        {
            for (int i = 0; i < 12; i++)
            {
                target[i] = source[i];
            }
        }

        private void ExecuteNonQuery(SqliteConnection connection, string sql)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        private void RefreshDataBindings()
        {
            _productSource.ResetBindings(false);
            _customerSource.ResetBindings(false);
            _transactionSource.ResetBindings(false);
            _cmbProducts.DataSource = null;
            _cmbProducts.DataSource = _productSource;
            _cmbProducts.DisplayMember = "Name";
            _cmbProducts.ValueMember = "Id";
            _cmbCustomers.DataSource = null;
            _cmbCustomers.DataSource = _customerSource;
            _cmbCustomers.DisplayMember = "FullName";
            _cmbCustomers.ValueMember = "Id";
        }

        private SplitContainer CreateSplitContainer()
        {
            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.SplitterDistance = 800;
            return split;
        }

        private DataGridView CreateGrid()
        {
            DataGridView grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AutoGenerateColumns = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            return grid;
        }

        private TableLayoutPanel CreateEditorPanel(int rowCount)
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 2;
            panel.RowCount = rowCount;
            panel.Padding = new Padding(10);
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));

            for (int i = 0; i < rowCount; i++)
            {
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            }

            return panel;
        }

        private Label CreateLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }

        private Button CreateButton(string text, EventHandler clickHandler)
        {
            Button button = new Button();
            button.Text = text;
            button.Dock = DockStyle.Fill;
            button.Click += clickHandler;
            return button;
        }

        private ToolStripMenuItem CreateMenuItem(string text, EventHandler clickHandler)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Click += clickHandler;
            return item;
        }

        private Label CreateInfoLabel()
        {
            Label label = new Label();
            label.Width = 300;
            label.Height = 45;
            label.BorderStyle = BorderStyle.FixedSingle;
            label.Padding = new Padding(8);
            label.Margin = new Padding(8);
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }
    }
}
