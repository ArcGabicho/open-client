package models

type Cliente struct {
	ID            string `json:"id"`
	Nombre        string `json:"nombre"`
	Fecha         string `json:"fecha"`
	Empresa       string `json:"empresa"`
	RazonSocial   string `json:"razon_social"`
	Rubro         string `json:"rubro"`
	TipoCliente   string `json:"tipo_cliente"`
	MedioContacto string `json:"medio_contacto"`
	Comentario    string `json:"comentario"`
	Etapa         string `json:"etapa"`
	Nombres       string `json:"nombres"`
	Apellidos     string `json:"apellidos"`
	Cargo         string `json:"cargo"`
	RUC           string `json:"ruc"`
	Telefono      string `json:"telefono"`
	Email         string `json:"email"`
	PaginaWeb     string `json:"pagina_web"`
	Direccion     string `json:"direccion"`
	Distrito      string `json:"distrito"`
	Provincia     string `json:"provincia"`
}