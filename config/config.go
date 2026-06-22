package config

import "os"

type Config struct {
	Port   string
	DBPath string
}

func Load() *Config {
	port := os.Getenv("PORT")
	if port == "" {
		port = "3000"
	}

	dbPath := os.Getenv("DATABASE_PATH")
	if dbPath == "" {
		dbPath = "./data/database.sqlite"
	}

	return &Config{
		Port:   port,
		DBPath: dbPath,
	}
}