import type { Order, OrderStatus } from '../types';

const steps: { key: OrderStatus[]; label: string }[] = [
  { key: ['Pending'], label: 'Commande reçue' },
  { key: ['Confirmed', 'Reserved'], label: 'Stock réservé' },
  { key: ['ReadyForPickup'], label: 'Prête en magasin' },
  { key: ['Collected'], label: 'Retirée' },
];

const order: OrderStatus[] = ['Pending', 'Confirmed', 'Reserved', 'ReadyForPickup', 'Collected'];

export function OrderTimeline({ value }: { value: Order }) {
  const failed = ['Rejected', 'Cancelled', 'Expired'].includes(value.status);
  const reached = (s: OrderStatus[]) => !failed && s.some((k) => order.indexOf(k) <= order.indexOf(value.status));

  return (
    <ol className={`timeline ${failed ? 'failed' : ''}`}>
      {steps.map((step) => (
        <li key={step.label} className={reached(step.key) ? 'done' : ''}>
          <span className="bullet" />
          {step.label}
        </li>
      ))}
    </ol>
  );
}
