import { statusLabels, type OrderStatus } from '../types';

export function StatusBadge({ status }: { status: OrderStatus }) {
  return <span className={`status status-${status}`}>{statusLabels[status]}</span>;
}
