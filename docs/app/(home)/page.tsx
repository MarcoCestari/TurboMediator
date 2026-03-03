import Link from 'next/link';

export default function HomePage() {
  return (
    <main className="flex flex-1 flex-col items-center justify-center text-center px-4 py-16">
      <div className="max-w-3xl mx-auto">
        <div className="inline-flex items-center rounded-full border px-3 py-1 text-sm mb-6 text-fd-muted-foreground">
          Source Generated &middot; Zero Reflection &middot; Native AOT
        </div>
        <h1 className="text-5xl font-extrabold tracking-tight mb-4 bg-gradient-to-r from-blue-600 to-purple-600 bg-clip-text text-transparent">
          TurboMediator
        </h1>
        <p className="text-xl text-fd-muted-foreground mb-8 max-w-2xl mx-auto">
          A high-performance Mediator library for .NET using Source Generators.
          Compile-time validated, Native AOT compatible, with a rich ecosystem of pipeline behaviors.
        </p>
        <div className="flex flex-wrap gap-4 justify-center mb-12">
          <Link
            href="/docs"
            className="inline-flex items-center justify-center rounded-lg bg-fd-primary px-6 py-3 text-sm font-medium text-fd-primary-foreground shadow hover:bg-fd-primary/90 transition-colors"
          >
            Get Started
          </Link>
          <Link
            href="/docs/core/messages"
            className="inline-flex items-center justify-center rounded-lg border px-6 py-3 text-sm font-medium shadow-sm hover:bg-fd-accent transition-colors"
          >
            Browse API
          </Link>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6 text-left">
          <div className="rounded-lg border p-6">
            <div className="text-2xl mb-2">⚡</div>
            <h3 className="font-semibold mb-1">High Performance</h3>
            <p className="text-sm text-fd-muted-foreground">
              Source Generator creates compile-time optimized dispatch code. No reflection, no <code>Activator.CreateInstance</code>.
            </p>
          </div>
          <div className="rounded-lg border p-6">
            <div className="text-2xl mb-2">🔒</div>
            <h3 className="font-semibold mb-1">Type Safe</h3>
            <p className="text-sm text-fd-muted-foreground">
              Full compile-time validation with build errors for missing handlers, duplicate handlers, and signature mismatches.
            </p>
          </div>
          <div className="rounded-lg border p-6">
            <div className="text-2xl mb-2">🧩</div>
            <h3 className="font-semibold mb-1">Rich Ecosystem</h3>
            <p className="text-sm text-fd-muted-foreground">
              20+ modular packages covering resilience, observability, enterprise patterns, sagas, and more.
            </p>
          </div>
        </div>
      </div>
    </main>
  );
}
