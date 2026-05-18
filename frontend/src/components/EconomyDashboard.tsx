import React, { useEffect, useMemo, useState } from 'react';
import DatePicker from 'react-datepicker';
import 'react-datepicker/dist/react-datepicker.css';
import { expenseApi, getApiErrorMessage, vehicleApi } from '../api';
import { Expense, Vehicle } from '../types';
import './EconomyDashboard.css';

type PerVehicleEconomyRow = {
  vehicleId: number;
  vehicleName: string;
  purchaseDate: Date;
  sellDate: Date | null;
  expenseCountInRange: number;
  spentInRange: number;
  netProfitWhenSoldInRange: number | null;
};

const expenseTotal = (expense: Expense): number => {
  if (expense.type === 10) {
    return expense.amount;
  }

  return expense.amount + (expense.shipping ?? 0);
};

const EconomyDashboard: React.FC = () => {
  const today = new Date();
  const monthStart = new Date(today.getFullYear(), today.getMonth(), 1);

  const [startDate, setStartDate] = useState<Date | null>(monthStart);
  const [endDate, setEndDate] = useState<Date | null>(today);
  const [vehicles, setVehicles] = useState<Vehicle[]>([]);
  const [expenses, setExpenses] = useState<Expense[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string>('');

  useEffect(() => {
    const load = async () => {
      try {
        setLoading(true);
        setError('');

        const [vehicleResponse, expenseResponse] = await Promise.all([
          vehicleApi.getAll(),
          expenseApi.getAll(),
        ]);

        setVehicles(vehicleResponse.data);
        setExpenses(expenseResponse.data);
      } catch (loadError) {
        setError(getApiErrorMessage(loadError, 'Failed to load economy dashboard data.'));
      } finally {
        setLoading(false);
      }
    };

    load();
  }, []);

  const rangeError = useMemo(() => {
    if (!startDate || !endDate) {
      return 'Choose both start and end dates.';
    }

    const start = new Date(startDate);
    const end = new Date(endDate);
    if (start > end) {
      return 'Start date cannot be after end date.';
    }

    return '';
  }, [startDate, endDate]);

  const analytics = useMemo(() => {
    if (rangeError) {
      return null;
    }

    if (!startDate || !endDate) {
      return null;
    }

    const start = new Date(startDate);
    start.setHours(0, 0, 0, 0);

    const end = new Date(endDate);
    end.setHours(23, 59, 59, 999);

    const expensesInRange = expenses.filter((expense) => {
      const expenseDate = new Date(expense.date);
      return expenseDate >= start && expenseDate <= end;
    });

    const spentInRange = expensesInRange.reduce((sum, expense) => sum + expenseTotal(expense), 0);

    const boughtVehicles = vehicles.filter((vehicle) => {
      const purchaseDate = new Date(vehicle.purchaseDate);
      return purchaseDate >= start && purchaseDate <= end;
    });

    const soldVehicles = vehicles.filter((vehicle) => {
      if (!vehicle.sellDate || vehicle.sellPrice === undefined || vehicle.sellPrice === null) {
        return false;
      }

      const sellDate = new Date(vehicle.sellDate);
      return sellDate >= start && sellDate <= end;
    });

    const expensesByVehicle = new Map<number, Expense[]>();
    for (const expense of expenses) {
      const rows = expensesByVehicle.get(expense.vehicleId) ?? [];
      rows.push(expense);
      expensesByVehicle.set(expense.vehicleId, rows);
    }

    const netProfitInRange = soldVehicles.reduce((sum, vehicle) => {
      const vehicleExpenses = expensesByVehicle.get(vehicle.id) ?? [];
      const purchaseDate = new Date(vehicle.purchaseDate);
      const sellDate = new Date(vehicle.sellDate!);

      const ownershipExpenseTotal = vehicleExpenses
        .filter((expense) => {
          const expenseDate = new Date(expense.date);
          return expenseDate >= purchaseDate && expenseDate <= sellDate;
        })
        .reduce((vehicleSum, expense) => vehicleSum + expenseTotal(expense), 0);

      return sum + ((vehicle.sellPrice ?? 0) - vehicle.purchasePrice - ownershipExpenseTotal);
    }, 0);

    const vehicleRows: PerVehicleEconomyRow[] = vehicles
      .map((vehicle) => {
        const purchaseDate = new Date(vehicle.purchaseDate);
        const sellDate = vehicle.sellDate ? new Date(vehicle.sellDate) : null;
        const vehicleExpenses = expensesByVehicle.get(vehicle.id) ?? [];

        const expensesInVehicleRange = vehicleExpenses.filter((expense) => {
          const expenseDate = new Date(expense.date);
          return expenseDate >= start && expenseDate <= end;
        });

        const spentForVehicle = expensesInVehicleRange.reduce((sum, expense) => sum + expenseTotal(expense), 0);
        const boughtInRange = purchaseDate >= start && purchaseDate <= end;
        const soldInRange = Boolean(sellDate && sellDate >= start && sellDate <= end && vehicle.sellPrice !== undefined && vehicle.sellPrice !== null);

        let netProfitWhenSoldInRange: number | null = null;
        if (soldInRange && sellDate) {
          const ownershipExpenseTotal = vehicleExpenses
            .filter((expense) => {
              const expenseDate = new Date(expense.date);
              return expenseDate >= purchaseDate && expenseDate <= sellDate;
            })
            .reduce((sum, expense) => sum + expenseTotal(expense), 0);

          netProfitWhenSoldInRange = (vehicle.sellPrice ?? 0) - vehicle.purchasePrice - ownershipExpenseTotal;
        }

        return {
          vehicleId: vehicle.id,
          vehicleName: vehicle.nickname ? vehicle.nickname : `${vehicle.year} ${vehicle.make} ${vehicle.model}`,
          purchaseDate,
          sellDate,
          expenseCountInRange: expensesInVehicleRange.length,
          spentInRange: spentForVehicle,
          netProfitWhenSoldInRange,
        };
      })
      .filter((row) => (row.purchaseDate >= start && row.purchaseDate <= end) || (row.sellDate && row.sellDate >= start && row.sellDate <= end) || row.expenseCountInRange > 0)
      .sort((a, b) => b.spentInRange - a.spentInRange);

    return {
      spentInRange,
      boughtCount: boughtVehicles.length,
      soldCount: soldVehicles.length,
      netProfitInRange,
      expenseCount: expensesInRange.length,
      vehicleRows,
    };
  }, [endDate, expenses, rangeError, startDate, vehicles]);

  const formatCurrency = (amount: number) => new Intl.NumberFormat('sv-SE', {
    style: 'currency',
    currency: 'SEK',
  }).format(amount);

  if (loading) {
    return <div className="economy-loading">Loading economy dashboard...</div>;
  }

  return (
    <div className="economy-dashboard">
      <div className="economy-header">
        <h1>Economy Dashboard</h1>
        <p>Track spend, profit, and vehicle activity for any period.</p>
      </div>

      <div className="economy-filters">
        <label>
          Start date
          <DatePicker
            selected={startDate}
            onChange={(date: Date | null) => setStartDate(date)}
            dateFormat="yyyy-MM-dd"
            showMonthDropdown
            showYearDropdown
            dropdownMode="select"
            maxDate={endDate ?? undefined}
            placeholderText="Select date"
            className="datepicker-input"
          />
        </label>
        <label>
          End date
          <DatePicker
            selected={endDate}
            onChange={(date: Date | null) => setEndDate(date)}
            dateFormat="yyyy-MM-dd"
            showMonthDropdown
            showYearDropdown
            dropdownMode="select"
            minDate={startDate ?? undefined}
            maxDate={new Date()}
            placeholderText="Select date"
            className="datepicker-input"
          />
        </label>
      </div>

      {error && <div className="economy-alert economy-error">{error}</div>}
      {rangeError && <div className="economy-alert economy-warning">{rangeError}</div>}

      {analytics && (
        <>
          <div className="economy-kpis">
            <div className="economy-kpi-card">
              <span className="kpi-label">Spent in Period</span>
              <span className="kpi-value expense">{formatCurrency(analytics.spentInRange)}</span>
            </div>
            <div className="economy-kpi-card">
              <span className="kpi-label">Made in Period (Net Profit)</span>
              <span className={`kpi-value ${analytics.netProfitInRange >= 0 ? 'profit' : 'loss'}`}>
                {formatCurrency(analytics.netProfitInRange)}
              </span>
            </div>
            <div className="economy-kpi-card">
              <span className="kpi-label">Cars Bought</span>
              <span className="kpi-value neutral">{analytics.boughtCount}</span>
            </div>
            <div className="economy-kpi-card">
              <span className="kpi-label">Cars Sold</span>
              <span className="kpi-value neutral">{analytics.soldCount}</span>
            </div>
            <div className="economy-kpi-card">
              <span className="kpi-label">Expenses Logged</span>
              <span className="kpi-value neutral">{analytics.expenseCount}</span>
            </div>
          </div>

          <div className="economy-breakdown">
            <h2>Per-Car Breakdown</h2>
            {analytics.vehicleRows.length === 0 ? (
              <div className="economy-empty">No car activity in this date range.</div>
            ) : (
              <div className="economy-table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Car</th>
                      <th>Buy Date</th>
                      <th>Sell Date</th>
                      <th>Expense Count</th>
                      <th>Spent during period</th>
                      <th>Net Profit</th>
                    </tr>
                  </thead>
                  <tbody>
                    {analytics.vehicleRows.map((row) => (
                      <tr key={row.vehicleId}>
                        <td>{row.vehicleName}</td>
                        <td>{row.purchaseDate.toISOString().split('T')[0]}</td>
                        <td>{row.sellDate ? row.sellDate.toISOString().split('T')[0] : '-'}</td>
                        <td>{row.expenseCountInRange}</td>
                        <td>{formatCurrency(row.spentInRange)}</td>
                        <td>
                          {row.netProfitWhenSoldInRange === null
                            ? '-'
                            : formatCurrency(row.netProfitWhenSoldInRange)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </>
      )}
    </div>
  );
};

export default EconomyDashboard;