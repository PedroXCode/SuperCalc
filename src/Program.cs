using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ZaldrionEShopSimulator
{
    // Campo por el que se va a ordenar la lista de Items
    public enum SortField
    {
        Name = 1,
        Price = 2,
        Description = 3,
        Rating = 4,
        Date = 5
    }

    // Representa un artículo del e-shop
    public class Item : IComparable<Item>
    {
        public string Name { get; }
        public string Description { get; }
        public decimal Price { get; }
        public int Rating { get; }
        public DateTime Date { get; }

        public Item(string name, string description, decimal price, int rating, DateTime date)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? string.Empty;
            Price = price;
            Rating = rating;
            Date = date;
        }

        public override string ToString()
        {
            return $"{Name} - {Price.ToString("C2", CultureInfo.CurrentCulture)} " +
                   $"(Rating: {Rating}, Fecha: {Date:yyyy-MM-dd})";
        }

        // Orden natural: por nombre (ascendente)
        public int CompareTo(Item other)
        {
            if (other == null) return 1;
            return string.Compare(Name, other.Name, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (obj is not Item other) return false;

            return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase)
                   && Price == other.Price
                   && string.Equals(Description, other.Description, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name.ToLowerInvariant(), Price, Description.ToLowerInvariant());
        }
    }

    // Comparador para ordenar por distintos campos y dirección
    public class ItemComparer : IComparer<Item>
    {
        private readonly SortField field;
        private readonly bool ascending;

        public ItemComparer(SortField field, bool ascending)
        {
            this.field = field;
            this.ascending = ascending;
        }

        public int Compare(Item x, Item y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            int result = field switch
            {
                SortField.Name => string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase),
                SortField.Price => x.Price.CompareTo(y.Price),
                SortField.Description => string.Compare(x.Description, y.Description, StringComparison.OrdinalIgnoreCase),
                SortField.Rating => x.Rating.CompareTo(y.Rating),
                SortField.Date => x.Date.CompareTo(y.Date),
                _ => 0
            };

            return ascending ? result : -result;
        }
    }

    // Representa al cliente | presupuesto + carrito
    public class Customer
    {
        public decimal Budget { get; set; }
        public Dictionary<Item, int> Cart { get; }

        public Customer(decimal budget)
        {
            Budget = budget;
            Cart = new Dictionary<Item, int>();
        }

        public int GetQuantityInCart(Item item)
        {
            return Cart.TryGetValue(item, out int qty) ? qty : 0;
        }

        public void AddToCart(Item item, int quantity)
        {
            if (Cart.ContainsKey(item))
            {
                Cart[item] += quantity;
            }
            else
            {
                Cart[item] = quantity;
            }
        }

        public void RemoveFromCart(Item item, int quantity)
        {
            if (!Cart.ContainsKey(item))
            {
                return;
            }

            Cart[item] -= quantity;
            if (Cart[item] <= 0)
            {
                Cart.Remove(item);
            }
        }

        public decimal GetCartTotal()
        {
            decimal total = 0m;
            foreach (var kvp in Cart)
            {
                total += kvp.Key.Price * kvp.Value;
            }
            return total;
        }

        public decimal GetBalance()
        {
            return Budget - GetCartTotal();
        }

        public void ClearCart()
        {
            Cart.Clear();
        }
    }

    // Representa el E-Shop | inventario + cliente y reglas de negocio 
    public class EShop
    {
        public Dictionary<Item, int> Inventory { get; }
        public Customer Customer { get; }

        public EShop(Dictionary<Item, int> inventory, Customer customer)
        {
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            Customer = customer ?? throw new ArgumentNullException(nameof(customer));
        }

        public IEnumerable<Item> GetInventoryItems()
        {
            return Inventory.Keys;
        }

        public int GetStock(Item item)
        {
            return Inventory.TryGetValue(item, out int stock) ? stock : 0;
        }

        public bool TryAddToCart(Item item, int quantity, out string message)
        {
            if (!Inventory.ContainsKey(item))
            {
                message = "El artículo seleccionado no está en el inventario.";
                return false;
            }

            int inStock = GetStock(item);
            if (inStock <= 0)
            {
                message = "Este artículo está agotado.";
                return false;
            }

            if (quantity <= 0)
            {
                message = "La cantidad debe ser un entero positivo.";
                return false;
            }

            int alreadyInCart = Customer.GetQuantityInCart(item);
            if (alreadyInCart + quantity > inStock)
            {
                int available = inStock - alreadyInCart;
                message = available <= 0
                    ? "Ya tienes en el carrito todas las unidades disponibles de este artículo."
                    : $"Solo puedes añadir hasta {available} unidad(es) más de este artículo.";
                return false;
            }

            Customer.AddToCart(item, quantity);
            message = $"{quantity} unidad(es) de '{item.Name}' añadidas al carrito.";
            return true;
        }

        public bool TryRemoveFromCart(Item item, int quantity, out string message)
        {
            int inCart = Customer.GetQuantityInCart(item);
            if (inCart == 0)
            {
                message = "Este artículo no está en tu carrito.";
                return false;
            }

            if (quantity <= 0)
            {
                message = "La cantidad debe ser un entero positivo.";
                return false;
            }

            if (quantity > inCart)
            {
                message = $"Solo tienes {inCart} unidad(es) de este artículo en tu carrito.";
                return false;
            }

            Customer.RemoveFromCart(item, quantity);
            message = $"{quantity} unidad(es) de '{item.Name}' removidas del carrito.";
            return true;
        }

        public bool CanCheckout(out string message)
        {
            if (Customer.Cart.Count == 0)
            {
                message = "Tu carrito está vacío.";
                return false;
            }

            decimal total = Customer.GetCartTotal();
            if (total > Customer.Budget)
            {
                message = "El total del carrito excede tu presupuesto. Remueve algunos artículos.";
                return false;
            }

            foreach (var kvp in Customer.Cart)
            {
                int inStock = GetStock(kvp.Key);
                if (kvp.Value > inStock)
                {
                    message = $"No hay suficientes unidades de '{kvp.Key.Name}' en inventario. Reduce la cantidad o remuévelo.";
                    return false;
                }
            }

            message = string.Empty;
            return true;
        }

        public bool TryCheckout(out string message)
        {
            if (!CanCheckout(out message))
            {
                return false;
            }

            decimal total = Customer.GetCartTotal();

            // Actualizar presupuesto e inventario solo en checkout
            Customer.Budget -= total;
            foreach (var kvp in Customer.Cart)
            {
                Item item = kvp.Key;
                int qty = kvp.Value;
                int currentStock = GetStock(item);
                Inventory[item] = currentStock - qty;
            }

            Customer.ClearCart();
            message = "Compra realizada con éxito! Gracias por su compra.";
            return true;
        }
    }

    // Métodos de entrada robusta
    public static class InputHelper
    {
        public static int ReadIntInRange(string prompt, int min, int max)
        {
            Console.Write(prompt);
            string input = (Console.ReadLine() ?? string.Empty).Trim();
            int option;
            while (!int.TryParse(input, out option) || option < min || option > max)
            {
                Console.WriteLine("ERROR - Opción inválida.");
                Console.Write(prompt);
                input = (Console.ReadLine() ?? string.Empty).Trim();
            }
            return option;
        }

        public static decimal ReadDecimal(string prompt, decimal min, decimal max)
        {
            Console.Write(prompt);
            string input = (Console.ReadLine() ?? string.Empty).Trim();

            decimal value;
            while (!decimal.TryParse(
                       input,
                       NumberStyles.Number,
                       CultureInfo.InvariantCulture,
                       out value) ||
                   value < min || value > max)
            {
                Console.WriteLine("ERROR - Valor inválido.");
                Console.Write(prompt);
                input = (Console.ReadLine() ?? string.Empty).Trim();
            }

            return value;
        }

        public static string ReadOption(string prompt, string[] validOptions)
        {
            Console.Write(prompt);
            string option = (Console.ReadLine() ?? string.Empty).Trim().ToUpperInvariant();

            while (!validOptions.Contains(option))
            {
                Console.WriteLine("ERROR - Opción inválida.");
                Console.Write(prompt);
                option = (Console.ReadLine() ?? string.Empty).Trim().ToUpperInvariant();
            }

            return option;
        }

        public static bool ReadYesNo(string prompt)
        {
            string[] valid = { "Y", "N" };
            string answer = ReadOption(prompt + " (Y/N): ", valid);
            return answer == "Y";
        }

        public static void PressEnterToContinue()
        {
            Console.WriteLine();
            Console.WriteLine("Pulsa ENTER para continuar...");
            Console.ReadLine();
        }
    }

    // Controla la lógica de la app y la UX
    public class EShopApp
    {
        private enum Screen
        {
            Inventory,
            Cart
        }

        private readonly EShop shop;

        // Estado de la vista de inventario
        private List<Item> allInventoryItems = new();
        private List<Item> inventoryViewItems = new();
        private int inventoryPageSize = 5;
        private int inventoryPageIndex = 0;
        private SortField inventorySortField = SortField.Name;
        private bool inventorySortAscending = true;
        private string inventorySearchTerm = string.Empty;

        // Estado de la vista de carrito
        private List<Item> allCartItems = new();
        private List<Item> cartViewItems = new();
        private int cartPageSize = 5;
        private int cartPageIndex = 0;
        private SortField cartSortField = SortField.Name;
        private bool cartSortAscending = true;
        private string cartSearchTerm = string.Empty;

        private bool running = true;

        public EShopApp()
        {
            Console.OutputEncoding = Encoding.UTF8;
            ShowWelcomeScreen();

            decimal budget = AskForBudget();

            Dictionary<Item, int> inventory = LoadInventory();
            var customer = new Customer(budget);
            shop = new EShop(inventory, customer);

            allInventoryItems = shop.GetInventoryItems().ToList();
            inventoryViewItems = new List<Item>(allInventoryItems);
        }

        public void Run()
        {
            Screen currentScreen = Screen.Inventory;

            while (running)
            {
                if (currentScreen == Screen.Inventory)
                {
                    currentScreen = ShowInventoryScreen();
                }
                else
                {
                    currentScreen = ShowCartScreen();
                }
            }

            Console.Clear();
            Console.WriteLine("Gracias por usar Zaldrion Tecno Market. Hasta luego!");
        }

        private void ShowWelcomeScreen()
        {
            Console.Clear();
            Console.WriteLine("=====================================");
            Console.WriteLine("        Zaldrion Tecno Market        ");
            Console.WriteLine("=====================================");
            Console.WriteLine();
            Console.WriteLine("Bienvenido a la mejor tienda de tecnologia.");
            Console.WriteLine("Podrás navegar productos, agregarlos al carrito");
            Console.WriteLine("y completar una compra según tu presupuesto.");
            Console.WriteLine();
            Console.WriteLine("Pulsa ENTER para comenzar a comprar...");
            Console.ReadLine();
        }

        private decimal AskForBudget()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("Introduce tu presupuesto de compra.");
                decimal budget = InputHelper.ReadDecimal("Presupuesto (ej. 40.00): ", 0.01m, 1_000_000m);
                Console.WriteLine($"Ingresaste: {budget.ToString("C2", CultureInfo.CurrentCulture)}");

                if (InputHelper.ReadYesNo("Quieres usar este presupuesto?"))
                {
                    return budget;
                }

                Console.WriteLine("Vamos a intentarlo de nuevo.");
                InputHelper.PressEnterToContinue();
            }
        }

        // Intenta leer inventario desde archivo y si falla usa datos por defecto
        
        private Dictionary<Item, int> LoadInventory()
        {
            const string fileName = "inventory.txt";

            Dictionary<Item, int> fromFile = TryLoadInventoryFromFile(fileName);
            if (fromFile != null && fromFile.Count > 0)
            {
                return fromFile;
            }

            Console.WriteLine("Se utilizará un inventario por defecto.");
            InputHelper.PressEnterToContinue();
            return CreateDefaultInventory();
        }

        // Formato esperado del archivo =
        // Nombre;Descripción;Precio;Rating;Fecha;Stock
        // Ejemplo:
        // Mouse Gamer;Mouse RGB;24.99;4;2025-03-10;15
        private Dictionary<Item, int> TryLoadInventoryFromFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    Console.WriteLine($"INFO: No se encontró el archivo de inventario '{path}'.");
                    return null;
                }

                var inventory = new Dictionary<Item, int>();
                using (var reader = new StreamReader(path))
                {
                    string line;
                    int lineNumber = 0;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNumber++;
                        line = line.Trim();
                        if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                            continue;

                        string[] parts = line.Split(';');
                        if (parts.Length < 6)
                        {
                            Console.WriteLine($"ADVERTENCIA: La línea {lineNumber} es inválida y se omitirá.");
                            continue;
                        }

                        string name = parts[0].Trim();
                        string description = parts[1].Trim();

                        if (!decimal.TryParse(parts[2], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal price))
                        {
                            Console.WriteLine($"ADVERTENCIA: Precio inválido en la línea {lineNumber}. Se omitirá.");
                            continue;
                        }

                        if (!int.TryParse(parts[3], out int rating))
                        {
                            Console.WriteLine($"ADVERTENCIA: Rating inválido en la línea {lineNumber}. Se omitirá.");
                            continue;
                        }

                        if (!DateTime.TryParse(parts[4], CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                        {
                            Console.WriteLine($"ADVERTENCIA: Fecha inválida en la línea {lineNumber}. Se omitirá.");
                            continue;
                        }

                        if (!int.TryParse(parts[5], out int stock) || stock < 0)
                        {
                            Console.WriteLine($"ADVERTENCIA: Stock inválido en la línea {lineNumber}. Se omitirá.");
                            continue;
                        }

                        var item = new Item(name, description, price, rating, date);
                        inventory[item] = stock;
                    }
                }

                if (inventory.Count == 0)
                {
                    Console.WriteLine("INFO: No se encontraron artículos válidos en el archivo.");
                    return null;
                }

                Console.WriteLine($"INFO: Se cargaron {inventory.Count} artículo(s) desde '{path}'.");
                InputHelper.PressEnterToContinue();
                return inventory;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ADVERTENCIA: Error al leer el archivo de inventario: {ex.Message}");
                return null;
            }
        }

        // Inventario por defecto (propio)
        private Dictionary<Item, int> CreateDefaultInventory()
        {
            var inventory = new Dictionary<Item, int>();

            inventory.Add(
                new Item("Mouse Gamer", "Mouse óptico RGB con 6 botones programables.",
                    24.99m, 4, new DateTime(2025, 3, 10)),
                15);

            inventory.Add(
                new Item("Teclado Mecánico", "Teclado mecánico con switches blue para Gaming.",
                    54.99m, 5, new DateTime(2025, 1, 22)),
                10);

            inventory.Add(
                new Item("Headset USB", "Headset con micrófono para clases online y gaming.",
                    39.50m, 4, new DateTime(2024, 11, 5)),
                12);

            inventory.Add(
                new Item("Webcam 1080p", "Cámara web Full HD para videollamadas.",
                    29.99m, 4, new DateTime(2024, 9, 18)),
                8);

            inventory.Add(
                new Item("Silla Gamer", "Silla ergonómica para largas sesiones de estudio.",
                    189.99m, 5, new DateTime(2024, 8, 30)),
                5);

            inventory.Add(
                new Item("Tarjeta PSN", "Tarjeta de regalo digital para PlayStation Network.",
                    25.00m, 5, new DateTime(2025, 4, 1)),
                20);

            inventory.Add(
                new Item("Licencia Antivirus BitZalDefender", "Suscripción de 1 año para proteger tu PC.",
                    19.99m, 4, new DateTime(2025, 2, 14)),
                30);

            inventory.Add(
                new Item("USB 128GB", "Memoria USB 3.0 de 128GB.",
                    17.49m, 4, new DateTime(2024, 12, 1)),
                25);

            inventory.Add(
                new Item("Router Wi-Fi 6", "Router inalámbrico de última generación.",
                    99.99m, 4, new DateTime(2025, 5, 3)),
                7);

            inventory.Add(
                new Item("Laptop Stand", "Base ajustable para laptop que mejora tu postura.",
                    32.00m, 5, new DateTime(2024, 10, 12)),
                10);

            return inventory;
        }

        private Screen ShowInventoryScreen()
        {
            bool inScreen = true;
            Screen nextScreen = Screen.Inventory;

            while (running && inScreen)
            {
                RefreshInventoryView();
                Console.Clear();
                ShowHeader();

                Console.WriteLine("INVENTARIO");
                Console.WriteLine("----------");

                PrintInventoryTable();

                Console.WriteLine();
                if (!string.IsNullOrWhiteSpace(inventorySearchTerm))
                {
                    Console.WriteLine($"Filtro de búsqueda actual: \"{inventorySearchTerm}\"");
                }
                Console.WriteLine($"Orden actual: {inventorySortField} ({(inventorySortAscending ? "ascendente" : "descendente")})");
                Console.WriteLine($"Artículos por página: {inventoryPageSize}");
                Console.WriteLine();
                Console.WriteLine("[N] Página siguiente   [P] Página anterior   [J] Ir a página");
                Console.WriteLine("[B] Buscar             [L] Limpiar búsqueda   [O] Ordenar");
                Console.WriteLine("[V] Ver detalles       [A] Añadir al carrito");
                Console.WriteLine("[T] Tamaño página      [C] Ir al Carrito");
                Console.WriteLine("[H] Checkout           [E] Salir");

                string[] validOptions = { "N", "P", "J", "B", "L", "O", "V", "A", "T", "C", "H", "E" };
                string option = InputHelper.ReadOption("Elige una opción: ", validOptions);

                switch (option)
                {
                    case "N":
                        NextInventoryPage();
                        break;
                    case "P":
                        PreviousInventoryPage();
                        break;
                    case "J":
                        JumpToInventoryPage();
                        break;
                    case "B":
                        InventorySearch();
                        break;
                    case "L":
                        ClearInventorySearch();
                        break;
                    case "O":
                        InventorySortMenu();
                        break;
                    case "V":
                        InventoryViewItemDetails();
                        break;
                    case "A":
                        InventoryAddItemToCart();
                        break;
                    case "T":
                        ChangeInventoryPageSize();
                        break;
                    case "C":
                        inScreen = false;
                        nextScreen = Screen.Cart;
                        break;
                    case "H":
                        DoCheckout();
                        if (!running)
                        {
                            inScreen = false;
                        }
                        break;
                    case "E":
                        if (ConfirmExit())
                        {
                            running = false;
                            inScreen = false;
                        }
                        break;
                }
            }

            return nextScreen;
        }

        private Screen ShowCartScreen()
        {
            bool inScreen = true;
            Screen nextScreen = Screen.Inventory;

            while (running && inScreen)
            {
                RefreshCartView();
                Console.Clear();
                ShowHeader();

                Console.WriteLine("CARRITO");
                Console.WriteLine("-------");

                PrintCartTable();

                Console.WriteLine();
                if (!string.IsNullOrWhiteSpace(cartSearchTerm))
                {
                    Console.WriteLine($"Filtro de búsqueda actual en carrito: \"{cartSearchTerm}\"");
                }
                Console.WriteLine($"Orden actual: {cartSortField} ({(cartSortAscending ? "ascendente" : "descendente")})");
                Console.WriteLine($"Artículos por página: {cartPageSize}");
                Console.WriteLine();
                Console.WriteLine("[N] Página siguiente   [P] Página anterior   [J] Ir a página");
                Console.WriteLine("[B] Buscar             [L] Limpiar búsqueda   [O] Ordenar");
                Console.WriteLine("[V] Ver detalles       [R] Remover del carrito");
                Console.WriteLine("[T] Tamaño página      [I] Volver al Inventario");
                Console.WriteLine("[H] Checkout           [E] Salir");

                string[] validOptions = { "N", "P", "J", "B", "L", "O", "V", "R", "T", "I", "H", "E" };
                string option = InputHelper.ReadOption("Elige una opción: ", validOptions);

                switch (option)
                {
                    case "N":
                        NextCartPage();
                        break;
                    case "P":
                        PreviousCartPage();
                        break;
                    case "J":
                        JumpToCartPage();
                        break;
                    case "B":
                        CartSearch();
                        break;
                    case "L":
                        ClearCartSearch();
                        break;
                    case "O":
                        CartSortMenu();
                        break;
                    case "V":
                        CartViewItemDetails();
                        break;
                    case "R":
                        CartRemoveItem();
                        break;
                    case "T":
                        ChangeCartPageSize();
                        break;
                    case "I":
                        inScreen = false;
                        nextScreen = Screen.Inventory;
                        break;
                    case "H":
                        DoCheckout();
                        if (!running)
                        {
                            inScreen = false;
                        }
                        break;
                    case "E":
                        if (ConfirmExit())
                        {
                            running = false;
                            inScreen = false;
                        }
                        break;
                }
            }

            return nextScreen;
        }

        private void ShowHeader()
        {
            decimal budget = shop.Customer.Budget;
            decimal total = shop.Customer.GetCartTotal();
            decimal balance = shop.Customer.GetBalance();

            Console.WriteLine("=== Zaldrion Tecno Market ===");
            Console.WriteLine(
                $"Presupuesto: {budget.ToString("C2", CultureInfo.CurrentCulture)}   " +
                $"Total carrito: {total.ToString("C2", CultureInfo.CurrentCulture)}   " +
                $"Balance: {balance.ToString("C2", CultureInfo.CurrentCulture)}");
            Console.WriteLine();
        }

        private static int GetTotalPages(int totalItems, int pageSize)
        {
            if (pageSize <= 0) return 0;
            return (totalItems + pageSize - 1) / pageSize;
        }

        private void RefreshInventoryView()
        {
            allInventoryItems = shop.GetInventoryItems().ToList();

            IEnumerable<Item> query = allInventoryItems;

            if (!string.IsNullOrWhiteSpace(inventorySearchTerm))
            {
                string term = inventorySearchTerm.ToUpperInvariant();
                query = query.Where(item =>
                    item.Name.ToUpperInvariant().Contains(term) ||
                    item.Description.ToUpperInvariant().Contains(term));
            }

            var list = query.ToList();
            var comparer = new ItemComparer(inventorySortField, inventorySortAscending);
            list.Sort(comparer);
            inventoryViewItems = list;

            int totalPages = GetTotalPages(inventoryViewItems.Count, inventoryPageSize);
            if (inventoryPageIndex >= totalPages)
            {
                inventoryPageIndex = totalPages == 0 ? 0 : totalPages - 1;
            }
        }

        private void RefreshCartView()
        {
            allCartItems = shop.Customer.Cart.Keys.ToList();

            IEnumerable<Item> query = allCartItems;

            if (!string.IsNullOrWhiteSpace(cartSearchTerm))
            {
                string term = cartSearchTerm.ToUpperInvariant();
                query = query.Where(item =>
                    item.Name.ToUpperInvariant().Contains(term) ||
                    item.Description.ToUpperInvariant().Contains(term));
            }

            var list = query.ToList();
            var comparer = new ItemComparer(cartSortField, cartSortAscending);
            list.Sort(comparer);
            cartViewItems = list;

            int totalPages = GetTotalPages(cartViewItems.Count, cartPageSize);
            if (cartPageIndex >= totalPages)
            {
                cartPageIndex = totalPages == 0 ? 0 : totalPages - 1;
            }
        }

        private void PrintInventoryTable()
        {
            if (inventoryViewItems.Count == 0)
            {
                Console.WriteLine("No hay artículos para mostrar.");
                return;
            }

            int totalItems = inventoryViewItems.Count;
            int totalPages = GetTotalPages(totalItems, inventoryPageSize);
            int currentPage = totalPages == 0 ? 0 : inventoryPageIndex + 1;
            int startIndex = inventoryPageIndex * inventoryPageSize;
            int endIndex = Math.Min(startIndex + inventoryPageSize, totalItems);

            Console.WriteLine($"Página {currentPage} de {totalPages}  (artículos {startIndex + 1}-{endIndex} de {totalItems})");
            Console.WriteLine();
            Console.WriteLine("#  {0,-20} {1,10} {2,8} {3,12} {4,10} {5,8}",
                "Artículo", "Precio", "Rating", "Fecha", "Stock", "Carrito");

            for (int i = startIndex; i < endIndex; i++)
            {
                Item item = inventoryViewItems[i];
                int stock = shop.GetStock(item);
                int inCart = shop.Customer.GetQuantityInCart(item);
                Console.WriteLine("[{0}] {1,-20} {2,10} {3,8} {4,12} {5,10} {6,8}",
                    i - startIndex,
                    Truncate(item.Name, 20),
                    item.Price.ToString("C2", CultureInfo.CurrentCulture),
                    item.Rating,
                    item.Date.ToString("yyyy-MM-dd"),
                    stock,
                    inCart);
            }
        }

        private void PrintCartTable()
        {
            if (cartViewItems.Count == 0)
            {
                Console.WriteLine("Tu carrito está vacío.");
                return;
            }

            int totalItems = cartViewItems.Count;
            int totalPages = GetTotalPages(totalItems, cartPageSize);
            int currentPage = totalPages == 0 ? 0 : cartPageIndex + 1;
            int startIndex = cartPageIndex * cartPageSize;
            int endIndex = Math.Min(startIndex + cartPageSize, totalItems);

            Console.WriteLine($"Página {currentPage} de {totalPages}  (artículos {startIndex + 1}-{endIndex} de {totalItems})");
            Console.WriteLine();
            Console.WriteLine("#  {0,-20} {1,10} {2,8} {3,12}",
                "Artículo", "Precio", "Cant.", "Subtotal");

            for (int i = startIndex; i < endIndex; i++)
            {
                Item item = cartViewItems[i];
                int qty = shop.Customer.GetQuantityInCart(item);
                decimal subtotal = item.Price * qty;
                Console.WriteLine("[{0}] {1,-20} {2,10} {3,8} {4,12}",
                    i - startIndex,
                    Truncate(item.Name, 20),
                    item.Price.ToString("C2", CultureInfo.CurrentCulture),
                    qty,
                    subtotal.ToString("C2", CultureInfo.CurrentCulture));
            }
        }

        private void NextInventoryPage()
        {
            int totalPages = GetTotalPages(inventoryViewItems.Count, inventoryPageSize);
            if (inventoryPageIndex + 1 >= totalPages)
            {
                Console.WriteLine("Ya estás en la última página.");
                InputHelper.PressEnterToContinue();
            }
            else
            {
                inventoryPageIndex++;
            }
        }

        private void PreviousInventoryPage()
        {
            if (inventoryPageIndex == 0)
            {
                Console.WriteLine("Ya estás en la primera página.");
                InputHelper.PressEnterToContinue();
            }
            else
            {
                inventoryPageIndex--;
            }
        }

        private void JumpToInventoryPage()
        {
            int totalPages = GetTotalPages(inventoryViewItems.Count, inventoryPageSize);
            if (totalPages == 0)
            {
                Console.WriteLine("No hay páginas a las que ir.");
                InputHelper.PressEnterToContinue();
                return;
            }

            int page = InputHelper.ReadIntInRange($"Número de página (1-{totalPages}): ", 1, totalPages);
            inventoryPageIndex = page - 1;
        }

        private void NextCartPage()
        {
            int totalPages = GetTotalPages(cartViewItems.Count, cartPageSize);
            if (cartPageIndex + 1 >= totalPages)
            {
                Console.WriteLine("Ya estás en la última página.");
                InputHelper.PressEnterToContinue();
            }
            else
            {
                cartPageIndex++;
            }
        }

        private void PreviousCartPage()
        {
            if (cartPageIndex == 0)
            {
                Console.WriteLine("Ya estás en la primera página.");
                InputHelper.PressEnterToContinue();
            }
            else
            {
                cartPageIndex--;
            }
        }

        private void JumpToCartPage()
        {
            int totalPages = GetTotalPages(cartViewItems.Count, cartPageSize);
            if (totalPages == 0)
            {
                Console.WriteLine("No hay páginas a las que ir.");
                InputHelper.PressEnterToContinue();
                return;
            }

            int page = InputHelper.ReadIntInRange($"Número de página (1-{totalPages}): ", 1, totalPages);
            cartPageIndex = page - 1;
        }

        private void InventorySearch()
        {
            Console.Write("Texto a buscar en nombre o descripción (vacío para cancelar): ");
            string term = (Console.ReadLine() ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(term))
            {
                Console.WriteLine("Búsqueda cancelada.");
                InputHelper.PressEnterToContinue();
                return;
            }

            inventorySearchTerm = term;
            RefreshInventoryView();

            if (inventoryViewItems.Count == 0)
            {
                Console.WriteLine("No se encontraron resultados.");
            }
            else
            {
                Console.WriteLine($"Se encontraron {inventoryViewItems.Count} artículo(s).");
            }

            inventoryPageIndex = 0;
            InputHelper.PressEnterToContinue();
        }

        private void ClearInventorySearch()
        {
            if (string.IsNullOrWhiteSpace(inventorySearchTerm))
            {
                Console.WriteLine("No hay filtro de búsqueda activo.");
            }
            else
            {
                inventorySearchTerm = string.Empty;
                RefreshInventoryView();
                inventoryPageIndex = 0;
                Console.WriteLine("Filtro de búsqueda eliminado.");
            }

            InputHelper.PressEnterToContinue();
        }

        private void CartSearch()
        {
            if (allCartItems.Count == 0)
            {
                Console.WriteLine("Tu carrito está vacío. No hay nada que buscar.");
                InputHelper.PressEnterToContinue();
                return;
            }

            Console.Write("Texto a buscar en el carrito (vacío para cancelar): ");
            string term = (Console.ReadLine() ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(term))
            {
                Console.WriteLine("Búsqueda en carrito cancelada.");
                InputHelper.PressEnterToContinue();
                return;
            }

            cartSearchTerm = term;
            RefreshCartView();

            if (cartViewItems.Count == 0)
            {
                Console.WriteLine("No se encontraron resultados en tu carrito.");
            }
            else
            {
                Console.WriteLine($"Se encontraron {cartViewItems.Count} artículo(s) en tu carrito.");
            }

            cartPageIndex = 0;
            InputHelper.PressEnterToContinue();
        }

        private void ClearCartSearch()
        {
            if (string.IsNullOrWhiteSpace(cartSearchTerm))
            {
                Console.WriteLine("No hay filtro de búsqueda activo en el carrito.");
            }
            else
            {
                cartSearchTerm = string.Empty;
                RefreshCartView();
                cartPageIndex = 0;
                Console.WriteLine("Filtro de búsqueda en carrito eliminado.");
            }

            InputHelper.PressEnterToContinue();
        }

        private void InventorySortMenu()
        {
            Console.WriteLine("Ordenar inventario por:");
            Console.WriteLine(" 1 - Nombre (ascendente)");
            Console.WriteLine(" 2 - Nombre (descendente)");
            Console.WriteLine(" 3 - Precio (ascendente)");
            Console.WriteLine(" 4 - Precio (descendente)");
            Console.WriteLine(" 5 - Descripción (ascendente)");
            Console.WriteLine(" 6 - Descripción (descendente)");
            Console.WriteLine(" 7 - Rating (ascendente)");
            Console.WriteLine(" 8 - Rating (descendente)");
            Console.WriteLine(" 9 - Fecha (ascendente)");
            Console.WriteLine("10 - Fecha (descendente)");
            Console.WriteLine(" 0 - Cancelar");

            int choice = InputHelper.ReadIntInRange("Elige una opción: ", 0, 10);
            if (choice == 0)
            {
                Console.WriteLine("Orden cancelado.");
                InputHelper.PressEnterToContinue();
                return;
            }

            switch (choice)
            {
                case 1: inventorySortField = SortField.Name; inventorySortAscending = true; break;
                case 2: inventorySortField = SortField.Name; inventorySortAscending = false; break;
                case 3: inventorySortField = SortField.Price; inventorySortAscending = true; break;
                case 4: inventorySortField = SortField.Price; inventorySortAscending = false; break;
                case 5: inventorySortField = SortField.Description; inventorySortAscending = true; break;
                case 6: inventorySortField = SortField.Description; inventorySortAscending = false; break;
                case 7: inventorySortField = SortField.Rating; inventorySortAscending = true; break;
                case 8: inventorySortField = SortField.Rating; inventorySortAscending = false; break;
                case 9: inventorySortField = SortField.Date; inventorySortAscending = true; break;
                case 10: inventorySortField = SortField.Date; inventorySortAscending = false; break;
            }

            RefreshInventoryView();
            inventoryPageIndex = 0;
            Console.WriteLine("Inventario ordenado.");
            InputHelper.PressEnterToContinue();
        }

        private void CartSortMenu()
        {
            Console.WriteLine("Ordenar carrito por:");
            Console.WriteLine(" 1 - Nombre (ascendente)");
            Console.WriteLine(" 2 - Nombre (descendente)");
            Console.WriteLine(" 3 - Precio (ascendente)");
            Console.WriteLine(" 4 - Precio (descendente)");
            Console.WriteLine(" 5 - Descripción (ascendente)");
            Console.WriteLine(" 6 - Descripción (descendente)");
            Console.WriteLine(" 7 - Rating (ascendente)");
            Console.WriteLine(" 8 - Rating (descendente)");
            Console.WriteLine(" 9 - Fecha (ascendente)");
            Console.WriteLine("10 - Fecha (descendente)");
            Console.WriteLine(" 0 - Cancelar");

            int choice = InputHelper.ReadIntInRange("Elige una opción: ", 0, 10);
            if (choice == 0)
            {
                Console.WriteLine("Orden cancelado.");
                InputHelper.PressEnterToContinue();
                return;
            }

            switch (choice)
            {
                case 1: cartSortField = SortField.Name; cartSortAscending = true; break;
                case 2: cartSortField = SortField.Name; cartSortAscending = false; break;
                case 3: cartSortField = SortField.Price; cartSortAscending = true; break;
                case 4: cartSortField = SortField.Price; cartSortAscending = false; break;
                case 5: cartSortField = SortField.Description; cartSortAscending = true; break;
                case 6: cartSortField = SortField.Description; cartSortAscending = false; break;
                case 7: cartSortField = SortField.Rating; cartSortAscending = true; break;
                case 8: cartSortField = SortField.Rating; cartSortAscending = false; break;
                case 9: cartSortField = SortField.Date; cartSortAscending = true; break;
                case 10: cartSortField = SortField.Date; cartSortAscending = false; break;
            }

            RefreshCartView();
            cartPageIndex = 0;
            Console.WriteLine("Carrito ordenado.");
            InputHelper.PressEnterToContinue();
        }

        private void InventoryViewItemDetails()
        {
            if (inventoryViewItems.Count == 0)
            {
                Console.WriteLine("No hay artículos para ver.");
                InputHelper.PressEnterToContinue();
                return;
            }

            int totalItems = inventoryViewItems.Count;
            int startIndex = inventoryPageIndex * inventoryPageSize;
            int endIndex = Math.Min(startIndex + inventoryPageSize, totalItems);
            int countOnPage = endIndex - startIndex;

            Console.WriteLine($"Selecciona un artículo de la página actual (0-{countOnPage - 1}), o -1 para cancelar:");
            Console.Write("Tu opción: ");
            string input = (Console.ReadLine() ?? string.Empty).Trim();
            int choice;
            while (!int.TryParse(input, out choice) || choice < -1 || choice >= countOnPage)
            {
                Console.WriteLine("ERROR: Opción inválida.");
                Console.Write("Tu opción: ");
                input = (Console.ReadLine() ?? string.Empty).Trim();
            }

            if (choice == -1)
            {
                Console.WriteLine("Operación cancelada.");
                InputHelper.PressEnterToContinue();
                return;
            }

            Item selected = inventoryViewItems[startIndex + choice];
            ShowItemDetails(selected);
        }

        private void CartViewItemDetails()
        {
            if (cartViewItems.Count == 0)
            {
                Console.WriteLine("No hay artículos para ver.");
                InputHelper.PressEnterToContinue();
                return;
            }

            int totalItems = cartViewItems.Count;
            int startIndex = cartPageIndex * cartPageSize;
            int endIndex = Math.Min(startIndex + cartPageSize, totalItems);
            int countOnPage = endIndex - startIndex;

            Console.WriteLine($"Selecciona un artículo de la página actual (0-{countOnPage - 1}), o -1 para cancelar:");
            Console.Write("Tu opción: ");
            string input = (Console.ReadLine() ?? string.Empty).Trim();
            int choice;
            while (!int.TryParse(input, out choice) || choice < -1 || choice >= countOnPage)
            {
                Console.WriteLine("ERROR: Opción inválida.");
                Console.Write("Tu opción: ");
                input = (Console.ReadLine() ?? string.Empty).Trim();
            }

            if (choice == -1)
            {
                Console.WriteLine("Operación cancelada.");
                InputHelper.PressEnterToContinue();
                return;
            }

            Item selected = cartViewItems[startIndex + choice];
            ShowItemDetails(selected);
        }

        private void InventoryAddItemToCart()
        {
            if (inventoryViewItems.Count == 0)
            {
                Console.WriteLine("No hay artículos para añadir.");
                InputHelper.PressEnterToContinue();
                return;
            }

            int totalItems = inventoryViewItems.Count;
            int startIndex = inventoryPageIndex * inventoryPageSize;
            int endIndex = Math.Min(startIndex + inventoryPageSize, totalItems);
            int countOnPage = endIndex - startIndex;

            Console.WriteLine($"Selecciona un artículo para añadir (0-{countOnPage - 1}), o -1 para cancelar:");
            Console.Write("Tu opción: ");
            string input = (Console.ReadLine() ?? string.Empty).Trim();
            int choice;
            while (!int.TryParse(input, out choice) || choice < -1 || choice >= countOnPage)
            {
                Console.WriteLine("ERROR: Opción inválida.");
                Console.Write("Tu opción: ");
                input = (Console.ReadLine() ?? string.Empty).Trim();
            }

            if (choice == -1)
            {
                Console.WriteLine("Añadir al carrito cancelado.");
                InputHelper.PressEnterToContinue();
                return;
            }

            Item selected = inventoryViewItems[startIndex + choice];
            int stock = shop.GetStock(selected);

            if (stock <= 0)
            {
                Console.WriteLine("Este artículo está agotado y no se puede añadir al carrito.");
                InputHelper.PressEnterToContinue();
                return;
            }

            int alreadyInCart = shop.Customer.GetQuantityInCart(selected);
            int availableToAdd = stock - alreadyInCart;

            if (availableToAdd <= 0)
            {
                Console.WriteLine("Ya tienes en el carrito todas las unidades disponibles de este artículo.");
                InputHelper.PressEnterToContinue();
                return;
            }

            Console.WriteLine($"Hay {stock} unidad(es) en inventario.");
            Console.WriteLine($"Actualmente tienes {alreadyInCart} en tu carrito.");
            Console.WriteLine($"Puedes añadir hasta {availableToAdd}.");
            int qty = InputHelper.ReadIntInRange(
                $"¿Cuántas unidades de '{selected.Name}' quieres añadir? (1-{availableToAdd}, 0 para cancelar): ",
                0, availableToAdd);

            if (qty == 0)
            {
                Console.WriteLine("Añadir al carrito cancelado.");
                InputHelper.PressEnterToContinue();
                return;
            }

            if (!InputHelper.ReadYesNo($"¿Confirmas añadir {qty} unidad(es) de '{selected.Name}' al carrito?"))
            {
                Console.WriteLine("Añadir al carrito cancelado.");
                InputHelper.PressEnterToContinue();
                return;
            }

            if (shop.TryAddToCart(selected, qty, out string message))
            {
                Console.WriteLine(message);
            }
            else
            {
                Console.WriteLine("ERROR: " + message);
            }

            InputHelper.PressEnterToContinue();
        }

        private void CartRemoveItem()
        {
            if (cartViewItems.Count == 0)
            {
                Console.WriteLine("Tu carrito está vacío.");
                InputHelper.PressEnterToContinue();
                return;
            }

            int totalItems = cartViewItems.Count;
            int startIndex = cartPageIndex * cartPageSize;
            int endIndex = Math.Min(startIndex + cartPageSize, totalItems);
            int countOnPage = endIndex - startIndex;

            Console.WriteLine($"Selecciona un artículo para remover (0-{countOnPage - 1}), o -1 para cancelar:");
            Console.Write("Tu opción: ");
            string input = (Console.ReadLine() ?? string.Empty).Trim();
            int choice;
            while (!int.TryParse(input, out choice) || choice < -1 || choice >= countOnPage)
            {
                Console.WriteLine("ERROR: Opción inválida.");
                Console.Write("Tu opción: ");
                input = (Console.ReadLine() ?? string.Empty).Trim();
            }

            if (choice == -1)
            {
                Console.WriteLine("Remover del carrito cancelado.");
                InputHelper.PressEnterToContinue();
                return;
            }

            Item selected = cartViewItems[startIndex + choice];
            int inCart = shop.Customer.GetQuantityInCart(selected);

            Console.WriteLine($"Tienes {inCart} unidad(es) de '{selected.Name}' en tu carrito.");
            int qty = InputHelper.ReadIntInRange(
                $"¿Cuántas quieres remover? (1-{inCart}, 0 para cancelar): ",
                0, inCart);

            if (qty == 0)
            {
                Console.WriteLine("Remover del carrito cancelado.");
                InputHelper.PressEnterToContinue();
                return;
            }

            if (!InputHelper.ReadYesNo($"¿Confirmas remover {qty} unidad(es) de '{selected.Name}' del carrito?"))
            {
                Console.WriteLine("Remover del carrito cancelado.");
                InputHelper.PressEnterToContinue();
                return;
            }

            if (shop.TryRemoveFromCart(selected, qty, out string message))
            {
                Console.WriteLine(message);
            }
            else
            {
                Console.WriteLine("ERROR: " + message);
            }

            InputHelper.PressEnterToContinue();
        }

        private void ChangeInventoryPageSize()
        {
            Console.WriteLine($"Artículos por página actual: {inventoryPageSize}");
            Console.WriteLine("Introduce un nuevo tamaño entre 1 y 20 (0 para cancelar).");
            Console.Write("Nuevo tamaño: ");
            string input = (Console.ReadLine() ?? string.Empty).Trim();

            if (!int.TryParse(input, out int newSize) || newSize < 0 || newSize > 20)
            {
                Console.WriteLine("ERROR: Tamaño de página inválido.");
                InputHelper.PressEnterToContinue();
                return;
            }

            if (newSize == 0)
            {
                Console.WriteLine("Cambio de tamaño cancelado.");
                InputHelper.PressEnterToContinue();
                return;
            }

            inventoryPageSize = newSize;
            inventoryPageIndex = 0;
            Console.WriteLine($"Artículos por página cambiados a {inventoryPageSize}.");
            InputHelper.PressEnterToContinue();
        }

        private void ChangeCartPageSize()
        {
            Console.WriteLine($"Artículos por página actual: {cartPageSize}");
            Console.WriteLine("Introduce un nuevo tamaño entre 1 y 20 (0 para cancelar).");
            Console.Write("Nuevo tamaño: ");
            string input = (Console.ReadLine() ?? string.Empty).Trim();

            if (!int.TryParse(input, out int newSize) || newSize < 0 || newSize > 20)
            {
                Console.WriteLine("ERROR: Tamaño de página inválido.");
                InputHelper.PressEnterToContinue();
                return;
            }

            if (newSize == 0)
            {
                Console.WriteLine("Cambio de tamaño cancelado.");
                InputHelper.PressEnterToContinue();
                return;
            }

            cartPageSize = newSize;
            cartPageIndex = 0;
            Console.WriteLine($"Artículos por página cambiados a {cartPageSize}.");
            InputHelper.PressEnterToContinue();
        }

        private void ShowItemDetails(Item item)
        {
            Console.Clear();
            ShowHeader();

            Console.WriteLine("DETALLES DEL ARTÍCULO");
            Console.WriteLine("---------------------");
            Console.WriteLine($"Nombre     : {item.Name}");
            Console.WriteLine($"Precio     : {item.Price.ToString("C2", CultureInfo.CurrentCulture)}");
            Console.WriteLine($"Rating     : {item.Rating}");
            Console.WriteLine($"Fecha      : {item.Date:yyyy-MM-dd}");
            Console.WriteLine($"En Stock   : {shop.GetStock(item)}");
            Console.WriteLine($"En Carrito : {shop.Customer.GetQuantityInCart(item)}");
            Console.WriteLine();
            Console.WriteLine("Descripción:");
            Console.WriteLine(item.Description);
            InputHelper.PressEnterToContinue();
        }

        private void DoCheckout()
        {
            Console.Clear();
            ShowHeader();

            if (shop.Customer.Cart.Count == 0)
            {
                Console.WriteLine("Tu carrito está vacío. No hay nada que comprar.");
                InputHelper.PressEnterToContinue();
                return;
            }

            decimal total = shop.Customer.GetCartTotal();
            Console.WriteLine("CHECKOUT");
            Console.WriteLine("--------");
            Console.WriteLine($"Vas a comprar {shop.Customer.Cart.Count} artículo(s) distinto(s).");
            foreach (var kvp in shop.Customer.Cart)
            {
                Console.WriteLine($"- {kvp.Key.Name} x{kvp.Value} = {(kvp.Key.Price * kvp.Value).ToString("C2", CultureInfo.CurrentCulture)}");
            }
            Console.WriteLine();
            Console.WriteLine($"Total del carrito: {total.ToString("C2", CultureInfo.CurrentCulture)}");
            Console.WriteLine($"Presupuesto actual: {shop.Customer.Budget.ToString("C2", CultureInfo.CurrentCulture)}");
            Console.WriteLine($"Balance después de comprar: {(shop.Customer.Budget - total).ToString("C2", CultureInfo.CurrentCulture)}");
            Console.WriteLine();

            if (!InputHelper.ReadYesNo("¿Deseas proceder con la compra?"))
            {
                Console.WriteLine("Compra cancelada.");
                InputHelper.PressEnterToContinue();
                return;
            }

            if (shop.TryCheckout(out string message))
            {
                Console.WriteLine(message);
                Console.WriteLine($"Nuevo presupuesto: {shop.Customer.Budget.ToString("C2", CultureInfo.CurrentCulture)}");
                if (InputHelper.ReadYesNo("Quieres seguir comprando?"))
                {
                    Console.WriteLine("Volviendo al inventario...");
                }
                else
                {
                    Console.WriteLine("Gracias por tu compra. Saliendo de la tienda...");
                    running = false;
                }
            }
            else
            {
                Console.WriteLine("ERROR: " + message);
            }

            InputHelper.PressEnterToContinue();
        }

        private bool ConfirmExit()
        {
            if (InputHelper.ReadYesNo("Estás seguro de que quieres salir de la tienda?"))
            {
                Console.WriteLine("Saliendo de la tienda. ¡Hasta la próxima!");
                InputHelper.PressEnterToContinue();
                return true;
            }

            Console.WriteLine("Salida cancelada. Volviendo a la tienda.");
            InputHelper.PressEnterToContinue();
            return false;
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength - 3) + "...";
        }
    }

    public static class Program
    {
        public static void Main(string[] args)
        {
            var app = new EShopApp();
            app.Run();
        }
    }
}
