# 🚗 Car Budget Tracker

A car budget tracking application built with .NET and React. Available as a Windows desktop app and a self-hosted Docker container.

[![Latest Release](https://img.shields.io/github/v/release/niklas2233/car-budget-tracker?label=latest+release)](https://github.com/niklas2233/car-budget-tracker/releases/latest)  [Download for Windows](https://github.com/niklas2233/car-budget-tracker/releases/latest) · [Docker Hub](https://hub.docker.com/r/niklas2233/car-budget-tracker)

## Features

- **Vehicle Management**: Add, view, edit, and delete vehicles with photo attachments and colour tags
- **Expense Tracking**: Log all car-related expenses (fuel, maintenance, repairs, insurance, etc.) with receipt photos
- **Profit / Loss**: Automatically calculates profit or loss per vehicle based on purchase price, expenses, and sale price
- **Economy Dashboard**: Fleet-wide totals, filtering by date range, and per-vehicle breakdown table
- **Plate Lookup**: Fetch vehicle details automatically by licence plate (Sweden/Norway)
- **JSON Import / Export**: Back up and restore individual vehicles or your entire fleet
- **Dashboard Search & Sort**: Filter vehicles by name and sort by various criteria
- **Select Mode**: Multi-select vehicles for bulk export or bulk delete
- **Dark Mode**: Full light/dark theme with system-aware styling
- **Mobile Responsive**: Works on phones and tablets — accessible from any device on your local network
- **Sold Vehicles**: Mark vehicles as sold and toggle their visibility on the dashboard

## Technology Stack

### Backend
- **.NET 10.0** — Web API
- **Entity Framework Core** — ORM
- **SQLite** — Database (no separate database server needed)
- **Clean Architecture** — Core / Infrastructure / API layers
- **Playwright** — Headless browser for plate lookups

### Frontend
- **React 19** with TypeScript
- **Vite** — Build tool
- **React Router** — Client-side routing
- **Axios** — HTTP client
- **CSS3** — Styling with CSS custom properties for theming

### Desktop (Windows)
- **Electron** — Wraps the app as a native Windows executable
- **electron-builder** — Packages into installer and portable `.exe`

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

### Windows Desktop App (Easiest)

Download the latest release from the [Releases page](https://github.com/niklas2233/car-budget-tracker/releases/latest):

| File | Description |
|---|---|
| `CarBudget Setup x.x.x.exe` | Installer — installs to your user profile |
| `CarBudget x.x.x.exe` | Portable — run from anywhere, data stored next to the exe |

No prerequisites needed — the backend and all dependencies are bundled inside.

You can also install via **winget**:
```
winget install Niklas2233.CarBudget
```

### Prerequisites (Docker / Local)

- **.NET SDK 10.0+** — [Download](https://dotnet.microsoft.com/download)
- **Node.js 18+** — [Download](https://nodejs.org/)

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

#### Choosing Region

Set `region` in `docker-compose.yml` to control currency, distance unit, and number formatting. Defaults to `sweden` if omitted.

| Region | Value | Currency | Distance |
|--------|-------|----------|----------|
| Sweden | `sweden` | SEK | km |
| Norway | `norway` | NOK | km |
| Europe | `europe` | EUR | km |
| America | `america` | USD | km |
| USA | `usa` | USD | miles |
| Great Britain | `gb` | GBP | miles |
| Worldwide | `worldwide` | configurable | configurable |

**Worldwide region** lets you choose currency and distance unit freely:

```yaml
environment:
  region: worldwide
  currency: CHF      # any supported currency code
  unit: km           # km or miles
```

You can also override the currency independently for the other regions with the `currency` env var:

```yaml
environment:
  region: europe
  currency: CHF   # override to Swiss Franc
```

If you change `region` later, restart the container:

```powershell
docker compose restart app
```

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
   $env:region='europe'   # sweden | norway | europe | america | usa | gb | worldwide
   $env:currency='CHF'    # optional currency override
   $env:unit='km'         # km or miles (worldwide region only)
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
- [ ] Export to CSV / PDF
- [ ] Fuel economy tracking
- [ ] Maintenance reminders

## License

This project is licensed under the [GNU General Public License v3.0](LICENSE).

## Support

For issues or questions, please create an issue in the repository.
