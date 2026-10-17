import { useEffect, useState } from 'react';
import { api } from './api';
import { CustomerSpace } from './customer/CustomerSpace';
import { StaffSpace } from './staff/StaffSpace';
import type { Product, Store } from './types';

type Space = 'customer' | 'staff';

export default function App() {
  const [space, setSpace] = useState<Space>('customer');
  const [products, setProducts] = useState<Product[]>([]);
  const [stores, setStores] = useState<Store[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    Promise.all([api.products(), api.stores()])
      .then(([p, s]) => {
        setProducts(p);
        setStores(s);
      })
      .catch((e: Error) => setError(`Passerelle injoignable : ${e.message}`));
  }, []);

  return (
    <div className="app">
      <header className="topbar">
        <div className="brand">
          <span className="logo" aria-hidden>
            ◎
          </span>
          <div>
            <h1>OmniOrder</h1>
            <p>Commandes omnicanales · click &amp; collect · réservation en magasin</p>
          </div>
        </div>
        <nav className="switch" aria-label="Espace">
          <button type="button" className={space === 'customer' ? 'active' : ''} onClick={() => setSpace('customer')}>
            Espace client
          </button>
          <button type="button" className={space === 'staff' ? 'active' : ''} onClick={() => setSpace('staff')}>
            Espace vendeur
          </button>
        </nav>
      </header>

      {error && <div className="banner">{error}</div>}

      <main>
        {space === 'customer' ? <CustomerSpace products={products} stores={stores} /> : <StaffSpace products={products} stores={stores} />}
      </main>
    </div>
  );
}
