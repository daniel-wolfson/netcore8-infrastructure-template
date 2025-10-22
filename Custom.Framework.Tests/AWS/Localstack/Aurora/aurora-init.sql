-- ================================================
-- Aurora PostgreSQL Initialization Script
-- ================================================

-- Enable extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Create schema for application
CREATE SCHEMA IF NOT EXISTS app;

-- Set default schema
SET search_path TO app, public;

-- Create Customers table
CREATE TABLE IF NOT EXISTS app.customers (
    id SERIAL PRIMARY KEY,
    email VARCHAR(255) UNIQUE NOT NULL,
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    phone VARCHAR(20),
    balance DECIMAL(18, 2) DEFAULT 0.00,
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create index on email
CREATE INDEX IF NOT EXISTS idx_customers_email ON app.customers(email);
CREATE INDEX IF NOT EXISTS idx_customers_active ON app.customers(is_active);

-- Create Products table
CREATE TABLE IF NOT EXISTS app.products (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    sku VARCHAR(50) UNIQUE NOT NULL,
    price DECIMAL(18, 2) NOT NULL,
    quantity INTEGER DEFAULT 0,
    category VARCHAR(100),
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create index on SKU and category
CREATE INDEX IF NOT EXISTS idx_products_sku ON app.products(sku);
CREATE INDEX IF NOT EXISTS idx_products_category ON app.products(category);
CREATE INDEX IF NOT EXISTS idx_products_active ON app.products(is_active);

-- Create Orders table
CREATE TABLE IF NOT EXISTS app.orders (
    id SERIAL PRIMARY KEY,
    customer_id INTEGER NOT NULL REFERENCES app.customers(id),
    order_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    total_amount DECIMAL(18, 2) NOT NULL,
    status VARCHAR(50) DEFAULT 'Pending',
    shipping_address TEXT,
    payment_method VARCHAR(50),
    tracking_number VARCHAR(100),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create indexes for orders
CREATE INDEX IF NOT EXISTS idx_orders_customer_id ON app.orders(customer_id);
CREATE INDEX IF NOT EXISTS idx_orders_status ON app.orders(status);
CREATE INDEX IF NOT EXISTS idx_orders_date ON app.orders(order_date DESC);

-- Create Order Items table
CREATE TABLE IF NOT EXISTS app.order_items (
    id SERIAL PRIMARY KEY,
    order_id INTEGER NOT NULL REFERENCES app.orders(id) ON DELETE CASCADE,
    product_id INTEGER NOT NULL REFERENCES app.products(id),
    quantity INTEGER NOT NULL,
    unit_price DECIMAL(18, 2) NOT NULL,
    subtotal DECIMAL(18, 2) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create indexes for order items
CREATE INDEX IF NOT EXISTS idx_order_items_order_id ON app.order_items(order_id);
CREATE INDEX IF NOT EXISTS idx_order_items_product_id ON app.order_items(product_id);

-- Create trigger to update updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Add triggers to tables
DROP TRIGGER IF EXISTS update_customers_updated_at ON app.customers;
CREATE TRIGGER update_customers_updated_at
    BEFORE UPDATE ON app.customers
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS update_products_updated_at ON app.products;
CREATE TRIGGER update_products_updated_at
    BEFORE UPDATE ON app.products
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS update_orders_updated_at ON app.orders;
CREATE TRIGGER update_orders_updated_at
    BEFORE UPDATE ON app.orders
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Insert sample data for testing
INSERT INTO app.customers (email, first_name, last_name, phone, balance) VALUES
    ('john.doe@example.com', 'John', 'Doe', '+1-555-0101', 1000.00),
    ('jane.smith@example.com', 'Jane', 'Smith', '+1-555-0102', 500.00),
    ('bob.johnson@example.com', 'Bob', 'Johnson', '+1-555-0103', 750.50)
ON CONFLICT (email) DO NOTHING;

INSERT INTO app.products (name, description, sku, price, quantity, category) VALUES
    ('Laptop Pro', 'High-performance laptop', 'LAPTOP-001', 1299.99, 50, 'Electronics'),
    ('Wireless Mouse', 'Ergonomic wireless mouse', 'MOUSE-001', 29.99, 200, 'Accessories'),
    ('USB-C Cable', '6ft USB-C charging cable', 'CABLE-001', 14.99, 500, 'Accessories'),
    ('Monitor 27"', '4K Ultra HD monitor', 'MONITOR-001', 399.99, 30, 'Electronics'),
    ('Keyboard Mechanical', 'RGB mechanical keyboard', 'KEYBOARD-001', 89.99, 100, 'Accessories')
ON CONFLICT (sku) DO NOTHING;

-- Grant permissions
GRANT ALL PRIVILEGES ON SCHEMA app TO admin;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA app TO admin;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA app TO admin;

-- Display setup information
DO $$
BEGIN
    RAISE NOTICE '';
    RAISE NOTICE '================================================';
    RAISE NOTICE '? Aurora PostgreSQL Database Initialized';
    RAISE NOTICE '================================================';
    RAISE NOTICE '';
    RAISE NOTICE 'Database: auroradb';
    RAISE NOTICE 'Schema: app';
    RAISE NOTICE '';
    RAISE NOTICE 'Tables Created:';
    RAISE NOTICE '  - app.customers (% rows)', (SELECT COUNT(*) FROM app.customers);
    RAISE NOTICE '  - app.products (% rows)', (SELECT COUNT(*) FROM app.products);
    RAISE NOTICE '  - app.orders';
    RAISE NOTICE '  - app.order_items';
    RAISE NOTICE '';
    RAISE NOTICE 'Sample data loaded for testing.';
    RAISE NOTICE '================================================';
END $$;
