import React, { useEffect, useState } from 'react';
import { BrowserRouter as Router, Routes, Route, Link } from 'react-router-dom';
import Dashboard from './components/Dashboard';
import VehicleForm from './components/VehicleForm';
import VehicleDetails from './components/VehicleDetails';
import ExpenseForm from './components/ExpenseForm';
import LookupCachePage from './components/LookupCachePage';
import './App.css';

function App() {
  const [darkMode, setDarkMode] = useState<boolean>(() => {
    return localStorage.getItem('darkMode') === 'true';
  });

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', darkMode ? 'dark' : 'light');
    localStorage.setItem('darkMode', String(darkMode));
  }, [darkMode]);

  return (
    <Router>
      <div className="App">
        <header className="App-header">
          <Link to="/" className="App-home-link" aria-label="Go to dashboard">
            <h1>🚗 Car Budget Manager</h1>
            <p>Track your vehicle expenses like a pro</p>
          </Link>
          <button
            className="dark-mode-toggle"
            onClick={() => setDarkMode(d => !d)}
            aria-label="Toggle dark mode"
          >
            {darkMode ? '☀️ Light' : '🌙 Dark'}
          </button>
        </header>
        <main>
          <Routes>
            <Route path="/" element={<Dashboard />} />
            <Route path="/vehicles/new" element={<VehicleForm />} />
            <Route path="/vehicles/:vehicleKey/edit" element={<VehicleForm />} />
            <Route path="/vehicles/:vehicleKey" element={<VehicleDetails />} />
            <Route path="/vehicles/:vehicleKey/expenses/new" element={<ExpenseForm />} />
            <Route path="/vehicles/:vehicleKey/expenses/:expenseId/edit" element={<ExpenseForm />} />
            <Route path="/cached-plates" element={<LookupCachePage />} />
            <Route path="/cached-plates/:licensePlate" element={<LookupCachePage />} />
          </Routes>
        </main>
      </div>
    </Router>
  );
}

export default App;

