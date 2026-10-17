import { useState } from 'react';
import { categoryIcon, euro, type Product } from '../types';

interface Props {
  products: Product[];
  onAdd: (product: Product) => void;
}

export function Catalog({ products, onAdd }: Props) {
  const [category, setCategory] = useState<string | null>(null);
  const categories = [...new Set(products.map((p) => p.category))];
  const visible = category ? products.filter((p) => p.category === category) : products;

  return (
    <section className="card">
      <div className="card-header">
        <h2>Catalogue</h2>
        <div className="chips">
          <button type="button" className={category === null ? 'chip active' : 'chip'} onClick={() => setCategory(null)}>
            Tout
          </button>
          {categories.map((c) => (
            <button type="button" key={c} className={category === c ? 'chip active' : 'chip'} onClick={() => setCategory(c)}>
              {c}
            </button>
          ))}
        </div>
      </div>
      {products.length === 0 ? (
        <p className="empty">Chargement du catalogue…</p>
      ) : (
        <div className="grid">
          {visible.map((p) => (
            <article key={p.sku} className="product">
              <div className="product-visual" aria-hidden>
                {categoryIcon(p.category)}
              </div>
              <h3>{p.name}</h3>
              <p className="muted">{p.description}</p>
              <div className="product-footer">
                <strong>{euro.format(p.price)}</strong>
                <button type="button" onClick={() => onAdd(p)}>
                  Ajouter
                </button>
              </div>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}
