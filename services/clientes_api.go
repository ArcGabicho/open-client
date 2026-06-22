package services

import (
	"log"
	"os"
	"os/signal"
	"strconv"
	"syscall"
	"time"

	"open-client/config"

	"github.com/gofiber/fiber/v3"
	"github.com/gofiber/fiber/v3/middleware/cors"
	"github.com/gofiber/fiber/v3/middleware/limiter"
	"github.com/gofiber/fiber/v3/middleware/logger"
	"github.com/gofiber/fiber/v3/middleware/recover"
)

type ClientService struct {
	app *fiber.App
}

func New() *ClientService {
	app := fiber.New(fiber.Config{
		AppName: "Open Client API",
	})

	app.Use(logger.New(logger.Config{
		Format: "[${time}] ${ip} ${status} ${method} ${path} ${latency}\n",
	}))
	app.Use(recover.New())
	app.Use(cors.New())
	app.Use(limiter.New(limiter.Config{
		Max:        100,
		Expiration: 1 * time.Minute,
	}))

	s := &ClientService{app: app}
	s.registerRoutes()
	return s
}

func (s *ClientService) registerRoutes() {
	s.app.Get("/", s.healthCheck)

	v1 := s.app.Group("/api/v1")
	v1.Get("/clientes", s.listClientes)
	v1.Get("/clientes/:id", s.getCliente)
}

func (s *ClientService) Listen(addr string) error {
	errCh := make(chan error, 1)

	go func() {
		log.Printf("Server starting on %s", addr)
		if err := s.app.Listen(addr); err != nil {
			errCh <- err
		}
	}()

	quit := make(chan os.Signal, 1)
	signal.Notify(quit, os.Interrupt, syscall.SIGTERM)

	select {
	case <-quit:
		log.Println("Shutting down server...")
		return s.app.Shutdown()
	case err := <-errCh:
		return err
	}
}

func (s *ClientService) healthCheck(c fiber.Ctx) error {
	return c.JSON(fiber.Map{
		"status":  "ok",
		"service": "open-client",
	})
}

func (s *ClientService) listClientes(c fiber.Ctx) error {
	page, _ := strconv.Atoi(c.Query("page", "1"))
	limit, _ := strconv.Atoi(c.Query("limit", "20"))
	search := c.Query("search", "")

	if page < 1 {
		page = 1
	}
	if limit < 1 || limit > 100 {
		limit = 20
	}

	total, err := config.Count(search)
	if err != nil {
		return c.Status(500).JSON(fiber.Map{"error": err.Error()})
	}

	clientes, err := config.List(page, limit, search)
	if err != nil {
		return c.Status(500).JSON(fiber.Map{"error": err.Error()})
	}

	pages := total / limit
	if total%limit > 0 {
		pages++
	}

	return c.JSON(fiber.Map{
		"data": clientes,
		"pagination": fiber.Map{
			"page":  page,
			"limit": limit,
			"total": total,
			"pages": pages,
		},
	})
}

func (s *ClientService) getCliente(c fiber.Ctx) error {
	id := c.Params("id")
	if id == "" {
		return c.Status(400).JSON(fiber.Map{"error": "ID requerido"})
	}

	cliente, err := config.GetByID(id)
	if err != nil {
		return c.Status(404).JSON(fiber.Map{"error": "Cliente no encontrado"})
	}

	return c.JSON(fiber.Map{"data": cliente})
}