interface Props {
  code: string;
  label?: string;
}

/** Exibe um trecho de código/JSON em fonte monoespaçada. */
export function CodeBlock({ code, label }: Props) {
  return (
    <div className="code-block">
      {label && <div className="code-label">{label}</div>}
      <pre>
        <code>{code}</code>
      </pre>
    </div>
  );
}
