# 🚗 Car Budget Manager

A car budget tracking application built with .NET and React.

## Features

- **Vehicle Management**: Add, view, edit, and delete vehicles
- **Expense Tracking**: Track all car-related expenses (fuel, maintenance, repairs, insurance, etc.)
- **Dashboard**: View all vehicles with total costs and expense summaries
- **Detailed Reports**: See complete expense history for each vehicle
- **Modern UI**: Clean, responsive React interface

## Technology Stack

### Backend
- **.NET 10.0** - Web API
- **Entity Framework Core** - ORM
- **SQLite** - Database (no separate database server needed)
- **Clean Architecture** - Separation of concerns (Core, Infrastructure, API)

### Frontend
- **React 18** with TypeScript
- **React Router** - Client-side routing
- **Axios** - HTTP client
- **CSS3** - Styling

## Project Structure

```
CarBudget/
├── src/
│   ├── CarBudget.Api/          # Web API controllers and endpoints
│   ├── CarBudget.Core/         # Domain models and interfaces
│   └── CarBudget.Infrastructure/ # Data access and repositories
├── frontend/                    # React application
└── README.md
```

## Getting Started

### Prerequisites

- **.NET SDK 10.0+** - [Download](https://dotnet.microsoft.com/download)
- **Node.js 18+** - [Download](https://nodejs.org/)

### Running with Docker (Recommended)

1. Install **Docker** - [Download](https://www.docker.com/products/docker-desktop)
2. Navigate to the project root directory
3. Start the app:
   ```bash
   docker compose up -d --build
   ```
4. The app will be available at `http://localhost:2233`
5. API at `http://localhost:2233/api`
6. Swagger docs at `http://localhost:2233/swagger`

To stop the containers:
```bash
docker compose down
```

#### Customizing the Port

Edit `webui_port` in `docker-compose.yml`:

```yaml
environment:
  - webui_port=2233   # change this to your desired port
```

The port mapping updates automatically to match.

#### Persisting the Database

By default the database is stored inside the container at `/app/data/carbudget.db`.  
To persist it on the host, set the volume in `docker-compose.yml`:

```yaml
volumes:
  - /your/host/path:/app/data
```

### Running Locally (Without Docker)

#### Running the Backend API

1. Open a terminal in the project root directory
2. Run the API:
   ```powershell
   cd src/CarBudget.Api
   dotnet run
   ```
3. The API will start at `http://localhost:5000`
4. Swagger documentation available at `http://localhost:5000/swagger`

#### Running the Frontend

1. Open a **new** terminal in the project root directory
2. Navigate to frontend and start the development server:
   ```powershell
   cd frontend
   npm start
   ```
3. The app will open in your browser at `http://localhost:3000`

## Usage

1. **Add a Vehicle**
   - Click "Add New Vehicle" on the dashboard
   - Fill in vehicle details (make, model, year, purchase price, etc.)
   - Click "Add Vehicle"

2. **Add Expenses**
   - Click on a vehicle card to view details
   - Click "Add Expense"
   - Select expense type and fill in details
   - Click "Add Expense"

3. **View Statistics**
   - Dashboard shows total expenses and costs for each vehicle
   - Vehicle details page shows complete expense history

## API Endpoints

### Vehicles
- `GET /api/vehicles` - Get all vehicles
- `GET /api/vehicles/{id}` - Get vehicle by ID
- `POST /api/vehicles` - Create a new vehicle
- `PUT /api/vehicles/{id}` - Update a vehicle
- `DELETE /api/vehicles/{id}` - Delete a vehicle

### Expenses
- `GET /api/expenses` - Get all expenses
- `GET /api/expenses/vehicle/{vehicleId}` - Get expenses by vehicle
- `GET /api/expenses/{id}` - Get expense by ID
- `POST /api/expenses` - Create a new expense
- `PUT /api/expenses/{id}` - Update an expense
- `DELETE /api/expenses/{id}` - Delete an expense

## Database

The application uses SQLite, which creates a `carbudget.db` file automatically when you first run the API. No database setup required!

## Development

### Building the Backend
```powershell
dotnet build
```

### Running Backend Tests
```powershell
dotnet test
```

### Building Frontend for Production
```powershell
cd frontend
npm run build
```

## Future Enhancements

- [ ] User authentication and multi-user support
- [ ] Expense categories and budgets
- [ ] Charts and analytics
- [ ] Export to CSV/PDF
- [ ] Fuel economy tracking
- [ ] Maintenance reminders
- [ ] Mobile app

## License

This project is open source and available for personal use.

## Support

For issues or questions, please create an issue in the repository.