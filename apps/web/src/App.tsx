import './App.css';

const principles = [
  'Everything is a task.',
  'Every task has structured fields.',
  'Every task has a timeline.',
];

function App() {
  return (
    <main className="app">
      <section className="intro" aria-labelledby="page-title">
        <div>
          <h1 id="page-title">DumpTether</h1>
          <p>
            Initial monorepo scaffold for a personal task-and-note system built
            around structured tasks and evidence-backed timelines.
          </p>
        </div>

        <ul aria-label="Core product principles">
          {principles.map((principle) => (
            <li key={principle}>{principle}</li>
          ))}
        </ul>
      </section>
    </main>
  );
}

export default App;
