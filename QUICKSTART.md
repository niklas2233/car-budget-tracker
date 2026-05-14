# 🚀 Quick Start Guide

## Your Car Budget Manager is Ready!

This application is inspired by Sonarr/Radarr and uses the same architectural patterns.

### Architecture Overview

Your app follows **Clean Architecture** principles:

```
┌─────────────────────────────────────┐
│         Frontend (React)            │  ← User Interface
└────────────────┬────────────────────┘
				 │ HTTP/REST
┌────────────────▼────────────────────┐
│      API Layer (Controllers)        │  ← Entry Point
└────────────────┬────────────────────┘
				 │
┌────────────────▼────────────────────┐
│   Core Layer (Domain Models)        │  ← Business Logic
└────────────────┬────────────────────┘
				 │
┌────────────────▼────────────────────┐
│ Infrastructure (Data Access)        │  ← Database
└─────────────────────────────────────┘
```

## Option 1: Easy Start (Recommended)

Just double-click `start.ps1` in Windows Explorer!

This will:
- Start the backend API on http://localhost:5000
- Start the frontend on http://localhost:3000
- Open your browser automatically

## Option 2: Manual Start

### Terminal 1 - Start Backend:
```powershell
cd "src/CarBudget.Api"
dotnet run
```

### Terminal 2 - Start Frontend:
```powershell
cd frontend
npm start
```

## First Time Setup

No setup needed! The database is created automatically when you first run the app.

## What You Can Do

1. **Dashboard** - See all your vehicles at a glance
2. **Add Vehicles** - Track multiple cars, trucks, motorcycles
3. **Track Expenses** - Log fuel, maintenance, repairs, insurance, and more
4. **View Reports** - See detailed expense history for each vehicle
5. **Monitor Costs** - Track total ownership cost

## Features Like Sonarr/Radarr

- **RESTful API** - Clean API endpoints for all operations
- **Separation of Concerns** - Core, Infrastructure, and API layers
- **Repository Pattern** - Data access abstraction
- **Entity Framework** - Database ORM
- **Swagger Documentation** - API docs at http://localhost:5000/swagger
- **Modern Frontend** - React-based UI
- **SQLite Database** - No external database needed

## Expense Types Supported

- 🛢️ **Fuel** - Gas and diesel purchases
- 🔧 **Maintenance** - Oil changes, tire rotations, etc.
- 🛠️ **Repair** - Fixes and replacements
- 📋 **Insurance** - Insurance payments
- 📄 **Registration** - License and registration fees
- 🅿️ **Parking** - Parking fees
- 🛣️ **Tolls** - Highway tolls
- 🧼 **Wash** - Car washes
- ➕ **Other** - Miscellaneous expenses

## Tips

- The database file (`carbudget.db`) is created in the API folder
- All data persists between sessions
- You can access the Swagger UI to test the API directly
- The frontend automatically connects to the backend

## Troubleshooting

**"Failed to load vehicles"**
- Make sure the backend is running on port 5000
- Check the backend terminal for any errors

**Port already in use**
- Close any other apps using ports 5000 or 3000
- Or modify the ports in the configuration

**Frontend won't start**
- Run `npm install` in the frontend folder first

## Next Steps

1. Add your first vehicle
2. Start tracking expenses
3. Watch your total cost of ownership
4. Make informed decisions about your vehicles!

---

Need help? Check the README.md for detailed documentation.

Happy tracking! 🎉
