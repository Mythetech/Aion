namespace Aion.Web.Onboarding;

public static class SampleDatabase
{
    public const string Name = "sample_store";

    public static string[] GetSqliteSchema() =>
    [
        """
        CREATE TABLE "categories" (
            "id" INTEGER PRIMARY KEY,
            "name" TEXT NOT NULL,
            "description" TEXT
        )
        """,
        """
        CREATE TABLE "products" (
            "id" INTEGER PRIMARY KEY,
            "name" TEXT NOT NULL,
            "category_id" INTEGER NOT NULL,
            "price" REAL NOT NULL,
            "stock_quantity" INTEGER NOT NULL DEFAULT 0,
            "is_active" INTEGER NOT NULL DEFAULT 1,
            FOREIGN KEY ("category_id") REFERENCES "categories"("id")
        )
        """,
        """
        CREATE TABLE "customers" (
            "id" INTEGER PRIMARY KEY,
            "name" TEXT NOT NULL,
            "email" TEXT NOT NULL,
            "city" TEXT,
            "created_at" TEXT NOT NULL
        )
        """,
        """
        CREATE TABLE "orders" (
            "id" INTEGER PRIMARY KEY,
            "customer_id" INTEGER NOT NULL,
            "order_date" TEXT NOT NULL,
            "status" TEXT NOT NULL DEFAULT 'pending',
            "total" REAL NOT NULL DEFAULT 0,
            FOREIGN KEY ("customer_id") REFERENCES "customers"("id")
        )
        """,
        """
        CREATE TABLE "order_items" (
            "id" INTEGER PRIMARY KEY,
            "order_id" INTEGER NOT NULL,
            "product_id" INTEGER NOT NULL,
            "quantity" INTEGER NOT NULL,
            "unit_price" REAL NOT NULL,
            FOREIGN KEY ("order_id") REFERENCES "orders"("id"),
            FOREIGN KEY ("product_id") REFERENCES "products"("id")
        )
        """
    ];

    public static string[] GetPostgresSchema() =>
    [
        """
        CREATE TABLE "categories" (
            "id" serial PRIMARY KEY,
            "name" text NOT NULL,
            "description" text
        )
        """,
        """
        CREATE TABLE "products" (
            "id" serial PRIMARY KEY,
            "name" text NOT NULL,
            "category_id" integer NOT NULL,
            "price" numeric NOT NULL,
            "stock_quantity" integer NOT NULL DEFAULT 0,
            "is_active" boolean NOT NULL DEFAULT true,
            FOREIGN KEY ("category_id") REFERENCES "categories"("id")
        )
        """,
        """
        CREATE TABLE "customers" (
            "id" serial PRIMARY KEY,
            "name" text NOT NULL,
            "email" text NOT NULL,
            "city" text,
            "created_at" timestamp NOT NULL
        )
        """,
        """
        CREATE TABLE "orders" (
            "id" serial PRIMARY KEY,
            "customer_id" integer NOT NULL,
            "order_date" timestamp NOT NULL,
            "status" text NOT NULL DEFAULT 'pending',
            "total" numeric NOT NULL DEFAULT 0,
            FOREIGN KEY ("customer_id") REFERENCES "customers"("id")
        )
        """,
        """
        CREATE TABLE "order_items" (
            "id" serial PRIMARY KEY,
            "order_id" integer NOT NULL,
            "product_id" integer NOT NULL,
            "quantity" integer NOT NULL,
            "unit_price" numeric NOT NULL,
            FOREIGN KEY ("order_id") REFERENCES "orders"("id"),
            FOREIGN KEY ("product_id") REFERENCES "products"("id")
        )
        """
    ];

    public static string[] GetSeedData() =>
    [
        """
        INSERT INTO "categories" ("id", "name", "description") VALUES
        (1, 'Electronics', 'Phones, laptops, and accessories'),
        (2, 'Books', 'Fiction and non-fiction'),
        (3, 'Clothing', 'Apparel and accessories'),
        (4, 'Home & Garden', 'Furniture and decor'),
        (5, 'Sports', 'Equipment and gear')
        """,
        """
        INSERT INTO "products" ("id", "name", "category_id", "price", "stock_quantity", "is_active") VALUES
        (1, 'Wireless Headphones', 1, 79.99, 150, 1),
        (2, 'USB-C Hub', 1, 34.99, 300, 1),
        (3, 'Laptop Stand', 1, 49.99, 85, 1),
        (4, 'Mechanical Keyboard', 1, 129.99, 60, 1),
        (5, 'The Great Gatsby', 2, 12.99, 200, 1),
        (6, 'Clean Code', 2, 39.99, 120, 1),
        (7, 'Designing Data-Intensive Applications', 2, 44.99, 95, 1),
        (8, 'Cotton T-Shirt', 3, 19.99, 500, 1),
        (9, 'Running Shoes', 3, 89.99, 75, 1),
        (10, 'Winter Jacket', 3, 149.99, 40, 0),
        (11, 'Desk Lamp', 4, 29.99, 180, 1),
        (12, 'Plant Pot Set', 4, 24.99, 220, 1),
        (13, 'Yoga Mat', 5, 29.99, 160, 1),
        (14, 'Resistance Bands', 5, 15.99, 300, 1),
        (15, 'Water Bottle', 5, 12.99, 400, 1)
        """,
        """
        INSERT INTO "customers" ("id", "name", "email", "city", "created_at") VALUES
        (1, 'Alice Johnson', 'alice@example.com', 'Seattle', '2024-01-15'),
        (2, 'Bob Smith', 'bob@example.com', 'Portland', '2024-02-20'),
        (3, 'Charlie Davis', 'charlie@example.com', 'San Francisco', '2024-03-10'),
        (4, 'Diana Martinez', 'diana@example.com', 'Austin', '2024-04-05'),
        (5, 'Edward Wilson', 'edward@example.com', 'Denver', '2024-05-12'),
        (6, 'Fiona Brown', 'fiona@example.com', 'Seattle', '2024-06-01'),
        (7, 'George Kim', 'george@example.com', 'Portland', '2024-06-18'),
        (8, 'Hannah Lee', 'hannah@example.com', 'San Francisco', '2024-07-22'),
        (9, 'Ivan Patel', 'ivan@example.com', 'Austin', '2024-08-30'),
        (10, 'Julia Anderson', 'julia@example.com', 'Denver', '2024-09-15')
        """,
        """
        INSERT INTO "orders" ("id", "customer_id", "order_date", "status", "total") VALUES
        (1, 1, '2024-06-01', 'completed', 114.98),
        (2, 2, '2024-06-05', 'completed', 84.98),
        (3, 3, '2024-06-10', 'completed', 179.98),
        (4, 1, '2024-07-01', 'completed', 49.99),
        (5, 4, '2024-07-15', 'shipped', 129.99),
        (6, 5, '2024-07-20', 'completed', 57.97),
        (7, 6, '2024-08-01', 'completed', 89.99),
        (8, 2, '2024-08-10', 'completed', 39.99),
        (9, 7, '2024-08-15', 'completed', 164.98),
        (10, 3, '2024-09-01', 'shipped', 79.99),
        (11, 8, '2024-09-10', 'pending', 44.99),
        (12, 9, '2024-09-20', 'completed', 59.97),
        (13, 10, '2024-10-01', 'completed', 119.98),
        (14, 1, '2024-10-15', 'shipped', 69.98),
        (15, 4, '2024-10-20', 'pending', 29.99)
        """,
        """
        INSERT INTO "order_items" ("id", "order_id", "product_id", "quantity", "unit_price") VALUES
        (1, 1, 1, 1, 79.99),
        (2, 1, 2, 1, 34.99),
        (3, 2, 6, 1, 39.99),
        (4, 2, 7, 1, 44.99),
        (5, 3, 4, 1, 129.99),
        (6, 3, 3, 1, 49.99),
        (7, 4, 3, 1, 49.99),
        (8, 5, 4, 1, 129.99),
        (9, 6, 13, 1, 29.99),
        (10, 6, 14, 1, 15.99),
        (11, 6, 15, 1, 12.99),
        (12, 7, 9, 1, 89.99),
        (13, 8, 6, 1, 39.99),
        (14, 9, 1, 1, 79.99),
        (15, 9, 6, 1, 39.99),
        (16, 9, 7, 1, 44.99),
        (17, 10, 1, 1, 79.99),
        (18, 11, 7, 1, 44.99),
        (19, 12, 13, 1, 29.99),
        (20, 12, 14, 1, 15.99),
        (21, 12, 15, 1, 12.99),
        (22, 13, 9, 1, 89.99),
        (23, 13, 11, 1, 29.99),
        (24, 14, 2, 2, 34.99),
        (25, 15, 11, 1, 29.99)
        """
    ];

    public static string[] GetSampleQueries() =>
    [
        "SELECT * FROM products WHERE is_active = 1 ORDER BY price DESC",
        "SELECT c.name, COUNT(o.id) as order_count, SUM(o.total) as total_spent FROM customers c JOIN orders o ON c.id = o.customer_id GROUP BY c.name ORDER BY total_spent DESC",
        "SELECT cat.name as category, COUNT(p.id) as product_count, AVG(p.price) as avg_price FROM categories cat JOIN products p ON cat.id = p.category_id GROUP BY cat.name",
        "SELECT o.status, COUNT(*) as count, SUM(o.total) as revenue FROM orders o GROUP BY o.status"
    ];
}
