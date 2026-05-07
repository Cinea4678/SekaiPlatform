<script setup lang="ts">
import type { Component } from 'vue'
import type { TenantRole } from '@/api/auth'
import {
  BookOpen,
  ChevronLeft,
  ChevronRight,
  Database,
  FileUp,
  FolderOpen,
  Search,
  ServerCog,
  ShieldCheck,
  Users,
} from 'lucide-vue-next'
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { RouterView, useRoute } from 'vue-router'
import Navbar from '@/components/Navbar.vue'
import { useAuth } from '@/lib/auth'

interface NavigationItem {
  name: string
  path: string
  icon: Component
  roles?: TenantRole[]
}

const route = useRoute()
const { state, canAccessRole } = useAuth()
const sidebarOpen = ref(true)
const isMobile = ref(false)

const navigation: NavigationItem[] = [
  { name: '工作台', path: '/', icon: BookOpen },
  { name: '统一搜索', path: '/search', icon: Search },
  { name: '资产目录', path: '/assets', icon: FolderOpen },
  { name: '译文导入', path: '/import/translations', icon: FileUp },
  { name: '租户用户', path: '/admin/users', icon: Users, roles: ['admin', 'super_admin'] },
  { name: '同步任务', path: '/admin/sync', icon: ServerCog, roles: ['admin', 'super_admin'] },
  { name: '索引维护', path: '/admin/search-index', icon: ShieldCheck, roles: ['super_admin'] },
]

const visibleNavigation = computed(() => {
  return navigation.filter(item => canAccessRole(state.currentTenant?.role, item.roles))
})

function checkMobile() {
  isMobile.value = window.innerWidth < 1024
  sidebarOpen.value = !isMobile.value
}

function toggleSidebar() {
  sidebarOpen.value = !sidebarOpen.value
}

function closeSidebarOnMobile() {
  if (isMobile.value) {
    sidebarOpen.value = false
  }
}

function isActive(path: string) {
  if (path === '/') {
    return route.path === '/'
  }

  return route.path.startsWith(path)
}

onMounted(() => {
  checkMobile()
  window.addEventListener('resize', checkMobile)
})

onUnmounted(() => {
  window.removeEventListener('resize', checkMobile)
})
</script>

<template>
  <div class="flex h-screen bg-background">
    <div v-if="sidebarOpen && isMobile" class="fixed inset-0 z-40 bg-black/40 lg:hidden" @click="closeSidebarOnMobile" />

    <aside
      class="fixed z-50 flex h-full flex-col overflow-hidden border-r bg-card transition-all duration-300 lg:relative"
      :class="[
        isMobile ? (sidebarOpen ? 'w-64' : 'w-0') : (sidebarOpen ? 'w-64' : 'w-16'),
        isMobile && !sidebarOpen ? '-translate-x-full' : 'translate-x-0',
      ]"
    >
      <div class="flex items-center justify-between border-b p-4">
        <div v-if="sidebarOpen" class="flex items-center gap-2">
          <Database :size="20" class="text-primary" />
          <div>
            <h1 class="text-sm font-semibold">
              Sekai Platform
            </h1>
            <p class="text-xs text-muted-foreground">
              语言资产工作台
            </p>
          </div>
        </div>
        <button
          class="hidden rounded-md p-2 hover:bg-accent lg:block"
          :class="{ 'mx-auto': !sidebarOpen }"
          aria-label="Toggle sidebar"
          @click="toggleSidebar"
        >
          <ChevronRight v-if="!sidebarOpen" :size="20" />
          <ChevronLeft v-else :size="20" />
        </button>
      </div>

      <nav class="flex-1 space-y-1 overflow-y-auto p-4">
        <router-link
          v-for="item in visibleNavigation"
          :key="item.path"
          :to="item.path"
          class="flex items-center gap-2 rounded-md px-3 py-2 text-sm transition-colors"
          :class="[
            isActive(item.path)
              ? 'bg-primary text-primary-foreground'
              : 'hover:bg-accent hover:text-accent-foreground',
          ]"
          @click="closeSidebarOnMobile"
        >
          <component :is="item.icon" :size="20" />
          <span v-if="sidebarOpen">{{ item.name }}</span>
        </router-link>
      </nav>
    </aside>

    <div class="flex min-w-0 flex-1 flex-col overflow-hidden">
      <Navbar :on-toggle-sidebar="toggleSidebar" />

      <main class="flex-1 overflow-auto">
        <div class="p-4 md:p-8">
          <RouterView />
        </div>
      </main>
    </div>
  </div>
</template>
