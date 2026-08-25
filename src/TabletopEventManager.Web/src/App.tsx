import { Route, Routes } from 'react-router-dom';
import { CalendarPage } from './pages/CalendarPage';
import { RegistrationPage } from './pages/RegistrationPage';
import { HealthPage } from './pages/HealthPage';
import './styles.css';

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<CalendarPage />} />
      <Route path="/registration/:slug" element={<RegistrationPage />} />
      <Route path="/health" element={<HealthPage />} />
    </Routes>
  );
}
