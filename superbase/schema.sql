-- ProgramaPaso · esquema de Supabase
-- Pegalo completo en Supabase → SQL Editor → New query → Run

-- 1) Perfil del usuario: nombre y "capacidad" general (0 a 100)
create table if not exists public.profiles (
  id          uuid primary key references auth.users (id) on delete cascade,
  name        text not null check (char_length(name) between 2 and 40),
  ability     real not null default 50 check (ability between 0 and 100),
  created_at  timestamptz not null default now(),
  updated_at  timestamptz not null default now()
);

-- 2) Puntaje por nivel
create table if not exists public.level_progress (
  user_id     uuid not null references auth.users (id) on delete cascade,
  level       int  not null check (level between 1 and 20),
  best_score  int  not null default 0 check (best_score between 0 and 100),
  last_score  int  not null default 0 check (last_score between 0 and 100),
  attempts    int  not null default 0,
  updated_at  timestamptz not null default now(),
  primary key (user_id, level)
);

-- 3) Dominio por tema (0 a 100): con esto la app elige qué repasar
create table if not exists public.topic_mastery (
  user_id     uuid not null references auth.users (id) on delete cascade,
  topic       text not null,
  mastery     real not null default 50 check (mastery between 0 and 100),
  seen        int  not null default 0,
  updated_at  timestamptz not null default now(),
  primary key (user_id, topic)
);

-- 4) Seguridad: cada usuario solo ve y modifica SUS filas
alter table public.profiles       enable row level security;
alter table public.level_progress enable row level security;
alter table public.topic_mastery  enable row level security;

drop policy if exists "profiles: leer el propio"   on public.profiles;
drop policy if exists "profiles: crear el propio"  on public.profiles;
drop policy if exists "profiles: editar el propio" on public.profiles;
create policy "profiles: leer el propio"   on public.profiles for select to authenticated
  using ((select auth.uid()) = id);
create policy "profiles: crear el propio"  on public.profiles for insert to authenticated
  with check ((select auth.uid()) = id);
create policy "profiles: editar el propio" on public.profiles for update to authenticated
  using ((select auth.uid()) = id) with check ((select auth.uid()) = id);

drop policy if exists "level_progress: leer lo propio"   on public.level_progress;
drop policy if exists "level_progress: crear lo propio"  on public.level_progress;
drop policy if exists "level_progress: editar lo propio" on public.level_progress;
create policy "level_progress: leer lo propio"   on public.level_progress for select to authenticated
  using ((select auth.uid()) = user_id);
create policy "level_progress: crear lo propio"  on public.level_progress for insert to authenticated
  with check ((select auth.uid()) = user_id);
create policy "level_progress: editar lo propio" on public.level_progress for update to authenticated
  using ((select auth.uid()) = user_id) with check ((select auth.uid()) = user_id);

drop policy if exists "topic_mastery: leer lo propio"   on public.topic_mastery;
drop policy if exists "topic_mastery: crear lo propio"  on public.topic_mastery;
drop policy if exists "topic_mastery: editar lo propio" on public.topic_mastery;
create policy "topic_mastery: leer lo propio"   on public.topic_mastery for select to authenticated
  using ((select auth.uid()) = user_id);
create policy "topic_mastery: crear lo propio"  on public.topic_mastery for insert to authenticated
  with check ((select auth.uid()) = user_id);
create policy "topic_mastery: editar lo propio" on public.topic_mastery for update to authenticated
  using ((select auth.uid()) = user_id) with check ((select auth.uid()) = user_id);

-- Nadie sin sesión puede tocar nada
revoke all on public.profiles, public.level_progress, public.topic_mastery from anon;

-- ------------------------------------------------------------------
-- SOLO PARA PROBAR (opcional): desbloquear los niveles 1 a 8 en tu cuenta.
-- Reemplazá el correo por el tuyo y ejecutá este bloque aparte.
--
-- insert into public.level_progress (user_id, level, best_score, last_score, attempts)
-- select u.id, l, 100, 100, 1
-- from auth.users u, generate_series(1, 8) as l
-- where u.email = 'TU_CORREO@gmail.com'
-- on conflict (user_id, level) do update set best_score = 100;
-- ------------------------------------------------------------------
