package config

import (
	"database/sql"
	"fmt"
	"os"
	"path/filepath"

	"open-client/models"

	_ "modernc.org/sqlite"
)

var DB *sql.DB

func Init(dbPath string) error {
	if err := os.MkdirAll(filepath.Dir(dbPath), 0755); err != nil {
		return fmt.Errorf("create data dir: %w", err)
	}

	var err error
	DB, err = sql.Open("sqlite", dbPath)
	if err != nil {
		return fmt.Errorf("open db: %w", err)
	}

	DB.Exec("PRAGMA journal_mode=WAL")
	DB.Exec("PRAGMA synchronous=NORMAL")

	if err = DB.Ping(); err != nil {
		return fmt.Errorf("ping db: %w", err)
	}

	_, err = DB.Exec(`
		CREATE TABLE IF NOT EXISTS clientes (
			id TEXT PRIMARY KEY,
			nombre TEXT DEFAULT '',
			fecha TEXT DEFAULT '',
			empresa TEXT DEFAULT '',
			razon_social TEXT DEFAULT '',
			rubro TEXT DEFAULT '',
			tipo_cliente TEXT DEFAULT '',
			medio_contacto TEXT DEFAULT '',
			comentario TEXT DEFAULT '',
			etapa TEXT DEFAULT '',
			nombres TEXT DEFAULT '',
			apellidos TEXT DEFAULT '',
			cargo TEXT DEFAULT '',
			ruc TEXT DEFAULT '',
			telefono TEXT DEFAULT '',
			email TEXT DEFAULT '',
			pagina_web TEXT DEFAULT '',
			direccion TEXT DEFAULT '',
			distrito TEXT DEFAULT '',
			provincia TEXT DEFAULT ''
		)
	`)
	if err != nil {
		return fmt.Errorf("create table: %w", err)
	}

	return nil
}

func Count(filter string) (int, error) {
	var total int
	query := "SELECT COUNT(*) FROM clientes"
	args := []any{}

	if filter != "" {
		query += " WHERE nombre LIKE ? OR empresa LIKE ? OR ruc LIKE ?"
		like := "%" + filter + "%"
		args = []any{like, like, like}
	}

	err := DB.QueryRow(query, args...).Scan(&total)
	return total, err
}

func List(page, limit int, filter string) ([]models.Cliente, error) {
	offset := (page - 1) * limit

	query := `SELECT id, nombre, fecha, empresa, razon_social, rubro,
		tipo_cliente, medio_contacto, comentario, etapa,
		nombres, apellidos, cargo, ruc, telefono, email,
		pagina_web, direccion, distrito, provincia
		FROM clientes`

	args := []any{}
	if filter != "" {
		query += " WHERE nombre LIKE ? OR empresa LIKE ? OR ruc LIKE ?"
		like := "%" + filter + "%"
		args = append(args, like, like, like)
	}

	query += " ORDER BY id LIMIT ? OFFSET ?"
	args = append(args, limit, offset)

	rows, err := DB.Query(query, args...)
	if err != nil {
		return nil, err
	}
	defer rows.Close()

	var clientes []models.Cliente
	for rows.Next() {
		var c models.Cliente
		if err := rows.Scan(
			&c.ID, &c.Nombre, &c.Fecha, &c.Empresa, &c.RazonSocial,
			&c.Rubro, &c.TipoCliente, &c.MedioContacto, &c.Comentario,
			&c.Etapa, &c.Nombres, &c.Apellidos, &c.Cargo, &c.RUC,
			&c.Telefono, &c.Email, &c.PaginaWeb, &c.Direccion,
			&c.Distrito, &c.Provincia,
		); err != nil {
			return nil, err
		}
		clientes = append(clientes, c)
	}

	if clientes == nil {
		clientes = []models.Cliente{}
	}

	return clientes, rows.Err()
}

func GetByID(id string) (*models.Cliente, error) {
	c := &models.Cliente{}
	err := DB.QueryRow(`SELECT id, nombre, fecha, empresa, razon_social, rubro,
		tipo_cliente, medio_contacto, comentario, etapa,
		nombres, apellidos, cargo, ruc, telefono, email,
		pagina_web, direccion, distrito, provincia
		FROM clientes WHERE id = ?`, id).Scan(
		&c.ID, &c.Nombre, &c.Fecha, &c.Empresa, &c.RazonSocial,
		&c.Rubro, &c.TipoCliente, &c.MedioContacto, &c.Comentario,
		&c.Etapa, &c.Nombres, &c.Apellidos, &c.Cargo, &c.RUC,
		&c.Telefono, &c.Email, &c.PaginaWeb, &c.Direccion,
		&c.Distrito, &c.Provincia,
	)
	if err != nil {
		return nil, err
	}
	return c, nil
}