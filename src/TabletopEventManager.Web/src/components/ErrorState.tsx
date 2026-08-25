export function ErrorState({ message = 'Something went wrong.' }: { message?: string }) {
  return <p role="alert">{message}</p>;
}
