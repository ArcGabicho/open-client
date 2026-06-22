package main

import (
	"log"

	"open-client/config"
	"open-client/services"
)

func main() {
	cfg := config.Load()

	if err := config.Init(cfg.DBPath); err != nil {
		log.Fatalf("Error initializing database: %v", err)
	}

	api := services.New()

	log.Fatal(api.Listen(":" + cfg.Port))
}